"""Temporary native MO2 diagnostic for the isolated frontend restart check."""
from pathlib import Path
from html import escape
import mobase

class HealthRestart(mobase.IPluginDiagnose):
    marker = Path(__file__).parent / 'data' / 'frontend-health-restart.txt'
    def init(self, organizer):
        return Path(__file__).parent.parent.name == 'mo2-fnv-host'
    def name(self): return 'Frontend health restart verification'
    def localizedName(self): return self.name()
    def author(self): return 'MO2 frontend verification'
    def description(self): return 'Temporary diagnostic for the isolated restart check.'
    def version(self): return mobase.VersionInfo(1, 0, 0)
    def settings(self): return []
    def activeProblems(self): return [1] if self.marker.exists() else []
    def shortDescription(self, key): return 'Frontend diagnostic restart verification'
    def fullDescription(self, key):
        return 'Report from an original MO2 diagnostic extension: ' + escape(self.marker.read_text().strip())
    def hasGuidedFix(self, key): return False
    def startGuidedFix(self, key): pass

def createPlugin(): return HealthRestart()
