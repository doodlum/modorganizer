#!/usr/bin/env python3
"""Compile actual ProcessRunner routing with fake dependencies, not Windows acceptance.

The harness checks observable call order and spawn parameters. It does not compile
Qt, create Windows processes, or substitute for NATIVE_LAUNCH_VALIDATION.md.
"""
import argparse
from pathlib import Path
import shlex
import subprocess
import tempfile


PREAMBLE = r'''
#include <optional>
#include <string>
#include <vector>
#include <stdexcept>
#include <iostream>
using QString = struct String : std::string {
  using std::string::string;
  bool isEmpty() const { return empty(); }
  const String& filePath() const { return *this; }
  String absoluteDir() const { return "C:/associated application"; }
};
namespace env {
struct Association { QString executable, formattedCommandLine; };
Association association;
Association getAssociation(const QString&) { return association; }
}
namespace routing_log { void error(const char*) {} }
#define log routing_log
struct QWidget {};
struct QObject { static const char* tr(const char* s) { return s; } };
using MyException = std::runtime_error;
constexpr int INVALID_HANDLE_VALUE = -1;
std::vector<std::string> calls;
struct Parameters {
  QString binary = "C:/mods/Test tools/tool.exe";
  QString arguments = "\"argument with spaces\"";
  QString currentDirectory = "C:/mods/Test tools";
  QString steamAppID;
  bool hooked = false;
};
Parameters spawned;
int launchHandle = 42;
struct Handle {
  int value = INVALID_HANDLE_VALUE;
  void reset(int v) { value = v; }
  int get() const { return value; }
};
struct UI { QWidget* mainWindow() { return nullptr; } };
struct Profile { QString name() { return "test"; } };
struct Game { QString gameDirectory() const { return "C:/game"; } };
struct Settings {};
struct Core {
  Profile profile;
  Game game;
  Settings config;
  bool hasProfile = true, allow = true;
  Profile* currentProfile() { calls.push_back("profile"); return hasProfile ? &profile : nullptr; }
  bool beforeRun(const QString&, const QString&, const QString&, const QString&,
                 const QString&, const QString&) {
    calls.push_back("beforeRun"); return allow;
  }
  Game* managedGame() { return &game; }
  Settings& settings() { return config; }
};
bool steamAllowed = true, blacklistAllowed = true;
bool checkSteam(QWidget*, const Parameters&, const QString&, const QString&, Settings&) {
  calls.push_back("steam"); return steamAllowed;
}
bool checkBlacklist(QWidget*, const Parameters&, Settings&) {
  calls.push_back("blacklist"); return blacklistAllowed;
}
void adjustForVirtualized(const Game*, Parameters& sp, Settings&) {
  calls.push_back("virtualize"); sp.currentDirectory = "C:/game/Data";
}
int startBinary(QWidget*, const Parameters& sp) {
  calls.push_back("spawn"); spawned = sp; return launchHandle;
}
struct ProcessRunner {
  enum Results { Error, Running };
  Core m_core;
  UI* m_ui = nullptr;
  Parameters m_sp;
  Handle m_handle;
  QString m_profileName, m_customOverwrite, m_forcedLibraries;
  QString m_shellOpen;
  std::optional<Results> shellResult;
  void setBinary(const QString& value) { m_sp.binary = value; }
  void setArguments(const QString& value) { m_sp.arguments = value; }
  void setCurrentDirectory(const QString& value) { m_sp.currentDirectory = value; }
  bool shouldRunShell() const;
  Results run();
  std::optional<Results> runShell() { calls.push_back("shell"); return shellResult; }
  Results postRun() { calls.push_back("postRun"); return Running; }
  std::optional<Results> runBinary();
};
void require(bool condition, const char* message) {
  if (!condition) throw std::runtime_error(message);
}
'''

CASES = r'''
int main() {
  try {
    ProcessRunner dispatch;
    dispatch.m_core.hasProfile = false;
    require(dispatch.run() == ProcessRunner::Running, "argument launch dispatch failed");
    require(calls == std::vector<std::string>{"spawn", "postRun"},
            "argument launch was redirected through the path-only shell helper");
    require(spawned.arguments == dispatch.m_sp.arguments &&
            spawned.currentDirectory == dispatch.m_sp.currentDirectory,
            "dispatch lost arguments or physical working directory");
    calls.clear(); launchHandle = INVALID_HANDLE_VALUE;
    require(dispatch.run() == ProcessRunner::Error && calls == std::vector<std::string>{"spawn"},
            "dispatch waited after failed spawn");
    launchHandle = 42; calls.clear();
    ProcessRunner noArguments;
    noArguments.m_sp.arguments = "";
    require(noArguments.run() == ProcessRunner::Running &&
            calls == std::vector<std::string>{"shell", "postRun"},
            "no-argument shell route changed");
    calls.clear(); noArguments.shellResult = ProcessRunner::Error;
    require(noArguments.run() == ProcessRunner::Error && calls == std::vector<std::string>{"shell"},
            "shell failure did not propagate directly");
    calls.clear();
    ProcessRunner associated;
    associated.m_sp.hooked = true; associated.m_shellOpen = "C:/mods/document.txt";
    env::association = {"C:/associated application/editor.exe", "\"C:/mods/document.txt\""};
    require(associated.run() == ProcessRunner::Running && !associated.shouldRunShell() &&
            spawned.binary == env::association.executable &&
            spawned.arguments == env::association.formattedCommandLine && spawned.hooked,
            "hooked association did not preserve executable and formatted arguments");
    require(calls == std::vector<std::string>{"profile", "beforeRun", "steam", "blacklist", "virtualize", "spawn", "postRun"},
            "hooked association bypassed native launch preparation");
    calls.clear();
    ProcessRunner fallback;
    fallback.m_sp.hooked = true; fallback.m_shellOpen = "C:/mods/document.txt";
    env::association = {};
    require(fallback.run() == ProcessRunner::Running && !fallback.m_sp.hooked &&
            calls == std::vector<std::string>{"shell", "postRun"},
            "missing association did not retain unhooked shell fallback");
    calls.clear();
    std::cout << "PASS dispatch: argument-preserving route, no-argument shell route, early errors, hooked associations and fallback\n";
    ProcessRunner plain;
    plain.m_core.hasProfile = false;
    require(!plain.runBinary(), "plain launch failed");
    require(calls == std::vector<std::string>{"spawn"}, "plain launch prepared VFS or profile");
    require(!spawned.hooked && spawned.arguments == plain.m_sp.arguments &&
            spawned.binary == plain.m_sp.binary &&
            spawned.currentDirectory == plain.m_sp.currentDirectory,
            "plain launch changed arguments or physical paths");
    require(plain.m_handle.get() == 42, "plain launch lost process handle");
    calls.clear(); launchHandle = INVALID_HANDLE_VALUE;
    require(plain.runBinary() == ProcessRunner::Error, "failed spawn not reported");
    calls.clear(); launchHandle = 42;
    ProcessRunner hooked;
    hooked.m_sp.hooked = true;
    require(!hooked.runBinary(), "hooked launch failed");
    require(calls == std::vector<std::string>{"profile", "beforeRun", "steam", "blacklist", "virtualize", "spawn"},
            "hooked launch changed call order");
    require(spawned.hooked && spawned.currentDirectory == "C:/game/Data",
            "hooked launch lost virtualization");
    for (int gate = 0; gate < 3; ++gate) {
      calls.clear();
      hooked.m_core.allow = gate != 0;
      steamAllowed = gate != 1;
      blacklistAllowed = gate != 2;
      require(hooked.runBinary() == ProcessRunner::Error, "cancelled launch succeeded");
      for (const auto& call : calls)
        require(call != "spawn" && call != "virtualize", "cancelled launch continued");
    }
    std::cout << "PASS routing: physical paths/arguments, no plain-launch VFS setup, handles/errors, hooked order/cancellation\n";
    return 0;
  } catch (const std::exception& e) {
    std::cerr << "FAIL routing: " << e.what() << '\n'; return 1;
  }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cxx', default='c++', help='Compiler command, optionally with sandbox wrapper')
    parser.add_argument('--source', type=Path, help='Alternate processrunner.cpp for baseline comparison')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    source = (args.source or repo / 'src/processrunner.cpp').read_text()
    begin = source.index('std::optional<ProcessRunner::Results> ProcessRunner::runBinary()')
    end = source.index('\nbool ProcessRunner::shouldRefresh(', begin)
    dispatch_begin = source.index('bool ProcessRunner::shouldRunShell() const')
    dispatch_end = source.index('std::optional<ProcessRunner::Results> ProcessRunner::runShell()', dispatch_begin)
    with tempfile.TemporaryDirectory(prefix='launch-routing-', dir=repo / 'frontend/artifacts') as directory:
        cpp = Path(directory) / 'routing.cpp'
        exe = Path(directory) / 'routing'
        cpp.write_text(PREAMBLE + source[dispatch_begin:dispatch_end] + source[begin:end] + CASES)
        subprocess.run(shlex.split(args.cxx) + ['-std=c++17', '-Wall', '-Wextra', '-Werror', str(cpp), '-o', str(exe)], check=True)
        subprocess.run([str(exe)], check=True)


if __name__ == '__main__':
    main()
