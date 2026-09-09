/**
 * Parity runner - MO2 side.
 *
 * Feeds a shared fixture through the C++ health-check port and prints the same
 * canonical JSON the Vortex runner prints, so compare.mjs can diff them.
 *
 * This links the real implementation files out of src/healthcheck - it does not
 * reimplement anything:
 *   src/healthcheck/filedependencyresolver.cpp   (resolver)
 *   src/healthcheck/requirementsreportmapper.cpp (mapper)
 *
 * Hydrated file payloads are echoed back verbatim from the fixture, mirroring
 * Vortex, whose mapper also passes the hydrator's object straight through
 * (mapRequirementsReport.ts:250-259 and friends). Which file is chosen is still
 * fully compared - only the untouched display blob is passed through.
 */

#include "filedependencyresolver.h"
#include "requirementsreportmapper.h"

#include <QCoreApplication>
#include <QFile>
#include <QHash>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QString>
#include <QTextStream>

#include <cstdio>

using namespace HealthCheck;

namespace
{

  // Hydration blobs, keyed by file UID, echoed verbatim into the output.
  QHash<QString, QJsonObject> g_hydrationRaw;

  QJsonArray toJsonArray(const QStringList& values)
  {
    QJsonArray array;
    for (const QString& value : values) {
      array.append(value);
    }
    return array;
  }

  // Mirrors JSON.stringify dropping `undefined` properties: an absent optional
  // string in the fixture parses to a null QString and must not be emitted.
  void insertIfPresent(QJsonObject& object, const QString& key, const QString& value)
  {
    if (!value.isEmpty()) {
      object.insert(key, value);
    }
  }

  // The resolver's hydrated Candidate.
  // Vortex shape: checkFileLevelRequirements.ts:225-243 (toCandidate)
  QJsonObject candidateToJson(const Candidate& candidate)
  {
    QJsonObject object;
    object.insert(QStringLiteral("fileVersionUid"), candidate.fileVersionUid);
    object.insert(QStringLiteral("modUid"), candidate.modUid);
    object.insert(QStringLiteral("modFileId"), candidate.modFileId);
    object.insert(QStringLiteral("category"), candidate.category);
    object.insert(QStringLiteral("position"), candidate.position);
    object.insert(QStringLiteral("fileName"), candidate.fileName);
    object.insert(QStringLiteral("version"), candidate.version);
    object.insert(QStringLiteral("modName"), candidate.modName);
    insertIfPresent(object, QStringLiteral("modSummary"), candidate.modSummary);
    insertIfPresent(object, QStringLiteral("thumbnailUrl"), candidate.thumbnailUrl);
    object.insert(QStringLiteral("adultContent"), candidate.adultContent);
    return object;
  }

  // The mapper's IFileRequirementCandidate.
  // Vortex shape: mapRequirementsReport.ts:178-189 (toCandidate)
  QJsonObject requirementCandidateToJson(const RequirementCandidate& candidate)
  {
    QJsonObject object;
    object.insert(QStringLiteral("fileUID"), candidate.fileUID);
    object.insert(QStringLiteral("modUID"), candidate.modUID);
    object.insert(QStringLiteral("modName"), candidate.modName);
    insertIfPresent(object, QStringLiteral("modSummary"), candidate.modSummary);
    insertIfPresent(object, QStringLiteral("thumbnailUrl"), candidate.thumbnailUrl);
    object.insert(QStringLiteral("fileName"), candidate.fileName);
    object.insert(QStringLiteral("version"), candidate.version);
    object.insert(QStringLiteral("adultContent"), candidate.adultContent);
    return object;
  }

  QJsonObject rawHydration(const QString& fileUID)
  {
    return g_hydrationRaw.value(fileUID);
  }

  QJsonObject reportToJson(const FileRequirementsReport& report)
  {
    QJsonArray sources;
    for (const SourceResult& source : report.sources) {
      QJsonArray dependencies;
      for (const DependencyResult& dependency : source.dependencies) {
        QJsonArray branches;
        for (const DependencyBranch& branch : dependency.branches) {
          QJsonObject branchJson;
          branchJson.insert(QStringLiteral("modFileId"), branch.modFileId);
          branchJson.insert(QStringLiteral("satisfyingEnabled"),
                            toJsonArray(branch.satisfyingEnabled));
          branchJson.insert(QStringLiteral("satisfyingDisabled"),
                            toJsonArray(branch.satisfyingDisabled));
          branchJson.insert(QStringLiteral("satisfyingUninstalled"),
                            toJsonArray(branch.satisfyingUninstalled));
          branchJson.insert(QStringLiteral("wrongEnabled"),
                            toJsonArray(branch.wrongEnabled));
          branchJson.insert(QStringLiteral("wrongDisabled"),
                            toJsonArray(branch.wrongDisabled));
          if (branch.recommended.has_value()) {
            branchJson.insert(QStringLiteral("recommended"),
                              candidateToJson(*branch.recommended));
          }
          branches.append(branchJson);
        }

        QJsonObject dependencyJson;
        dependencyJson.insert(QStringLiteral("definitionId"), dependency.definitionId);
        dependencyJson.insert(QStringLiteral("branches"), branches);
        dependencies.append(dependencyJson);
      }

      QJsonObject sourceJson;
      sourceJson.insert(QStringLiteral("sourceFileVersionUid"),
                        source.sourceFileVersionUid);
      sourceJson.insert(QStringLiteral("dependencies"), dependencies);
      sources.append(sourceJson);
    }

    QJsonObject object;
    object.insert(QStringLiteral("sources"), sources);
    return object;
  }

  QJsonObject branchToJson(const RequirementBranch& branch)
  {
    QJsonObject object;
    object.insert(QStringLiteral("kind"), branchKindToString(branch.kind));
    object.insert(QStringLiteral("modFileId"), branch.modFileId);

    switch (branch.kind) {
    case BranchKind::Download:
      if (branch.candidate.has_value()) {
        object.insert(QStringLiteral("candidate"),
                      requirementCandidateToJson(*branch.candidate));
      }
      break;
    case BranchKind::Install:
      if (branch.uninstalledFile.has_value()) {
        object.insert(QStringLiteral("uninstalledFile"),
                      rawHydration(branch.uninstalledFile->fileUID));
      }
      break;
    case BranchKind::Enable:
      if (branch.correctFile.has_value()) {
        object.insert(QStringLiteral("correctFile"),
                      rawHydration(branch.correctFile->fileUID));
      }
      break;
    }

    if (branch.enabledFile.has_value()) {
      object.insert(QStringLiteral("enabledFile"),
                    rawHydration(branch.enabledFile->fileUID));
    }

    return object;
  }

  QJsonObject requirementToJson(const FileRequirement& requirement)
  {
    QJsonObject object;
    object.insert(QStringLiteral("kind"), kindToString(requirement.kind));
    object.insert(QStringLiteral("requirementDefId"), requirement.requirementDefId);

    switch (requirement.kind) {
    case RequirementKind::Missing:
      if (requirement.candidate.has_value()) {
        object.insert(QStringLiteral("candidate"),
                      requirementCandidateToJson(*requirement.candidate));
      }
      break;

    case RequirementKind::WrongVersionInstalled:
      if (requirement.installedFile.has_value()) {
        object.insert(QStringLiteral("installedFile"),
                      rawHydration(requirement.installedFile->fileUID));
      }
      if (requirement.candidate.has_value()) {
        object.insert(QStringLiteral("candidate"),
                      requirementCandidateToJson(*requirement.candidate));
      }
      break;

    case RequirementKind::WrongVersionEnabled:
      if (requirement.enabledFile.has_value()) {
        object.insert(QStringLiteral("enabledFile"),
                      rawHydration(requirement.enabledFile->fileUID));
      }
      if (requirement.correctFile.has_value()) {
        object.insert(QStringLiteral("correctFile"),
                      rawHydration(requirement.correctFile->fileUID));
      }
      break;

    case RequirementKind::CorrectVersionUninstalled:
      if (requirement.uninstalledFile.has_value()) {
        object.insert(QStringLiteral("uninstalledFile"),
                      rawHydration(requirement.uninstalledFile->fileUID));
      }
      if (requirement.enabledFile.has_value()) {
        object.insert(QStringLiteral("enabledFile"),
                      rawHydration(requirement.enabledFile->fileUID));
      }
      break;

    case RequirementKind::Or: {
      QJsonArray branches;
      for (const RequirementBranch& branch : requirement.branches) {
        branches.append(branchToJson(branch));
      }
      object.insert(QStringLiteral("branches"), branches);
      break;
    }
    }

    return object;
  }

  QJsonObject metadataToJson(const FileRequirementsMetadata& metadata)
  {
    QJsonObject fileRequirements;
    for (auto it = metadata.fileRequirements.constBegin();
         it != metadata.fileRequirements.constEnd(); ++it) {
      QJsonArray requirements;
      for (const FileRequirement& requirement : it.value().requirements) {
        requirements.append(requirementToJson(requirement));
      }

      QJsonObject entry;
      entry.insert(QStringLiteral("sourceFileUID"), it.value().sourceFileUID);
      entry.insert(QStringLiteral("sourceModName"), it.value().sourceModName);
      entry.insert(QStringLiteral("sourceModUID"), it.value().sourceModUID);
      entry.insert(QStringLiteral("requirements"), requirements);

      fileRequirements.insert(it.key(), entry);
    }

    QJsonObject object;
    object.insert(QStringLiteral("gameId"), metadata.gameId);
    object.insert(QStringLiteral("modsChecked"), metadata.modsChecked);
    object.insert(QStringLiteral("fileRequirements"), fileRequirements);
    object.insert(QStringLiteral("errors"), toJsonArray(metadata.errors));
    return object;
  }

  // --- fixture parsing ------------------------------------------------------

  CandidateRow parseCandidateRow(const QJsonObject& object)
  {
    CandidateRow row;
    row.sourceFileVersionUid =
        object.value(QStringLiteral("sourceFileVersionUid")).toString();
    row.definitionId   = object.value(QStringLiteral("definitionId")).toString();
    row.modFileId      = object.value(QStringLiteral("modFileId")).toString();
    row.fileVersionUid = object.value(QStringLiteral("fileVersionUid")).toString();
    row.position       = object.value(QStringLiteral("position")).toString();
    row.category       = object.value(QStringLiteral("category")).toInt();
    row.modStatus      = object.value(QStringLiteral("modStatus")).toString();
    row.modUid         = object.value(QStringLiteral("modUid")).toString();
    return row;
  }

  FileVersionDetail parseDetail(const QJsonObject& object)
  {
    FileVersionDetail detail;
    detail.fileVersionUid = object.value(QStringLiteral("fileVersionUid")).toString();
    detail.modUid         = object.value(QStringLiteral("modUid")).toString();
    detail.modFileId      = object.value(QStringLiteral("modFileId")).toString();
    detail.name           = object.value(QStringLiteral("name")).toString();
    detail.version        = object.value(QStringLiteral("version")).toString();
    return detail;
  }

  ModDetail parseModDetail(const QJsonObject& object)
  {
    ModDetail mod;
    mod.modUid       = object.value(QStringLiteral("modUid")).toString();
    mod.name         = object.value(QStringLiteral("name")).toString();
    mod.summary      = object.value(QStringLiteral("summary")).toString();
    mod.thumbnailUrl = object.value(QStringLiteral("thumbnailUrl")).toString();
    mod.adultContent = object.value(QStringLiteral("adultContent")).toBool();
    return mod;
  }

}  // namespace

int main(int argc, char* argv[])
{
  QCoreApplication app(argc, argv);

  if (argc < 2) {
    std::fputs("usage: mo2-parity-runner <fixture.json>\n", stderr);
    return 2;
  }

  QFile file(QString::fromLocal8Bit(argv[1]));
  if (!file.open(QIODevice::ReadOnly)) {
    std::fputs("cannot open fixture\n", stderr);
    return 2;
  }

  QJsonParseError parseError{};
  const QJsonDocument document = QJsonDocument::fromJson(file.readAll(), &parseError);
  if (parseError.error != QJsonParseError::NoError) {
    std::fprintf(stderr, "fixture parse error: %s\n",
                 qPrintable(parseError.errorString()));
    return 2;
  }
  const QJsonObject fixture = document.object();

  // --- inputs ---------------------------------------------------------------

  QList<InstalledFile> installedFiles;
  for (const QJsonValue& value :
       fixture.value(QStringLiteral("installedFiles")).toArray()) {
    const QJsonObject object = value.toObject();
    InstalledFile installed;
    installed.fileVersionUid = object.value(QStringLiteral("fileVersionUid")).toString();
    installed.enabled        = object.value(QStringLiteral("enabled")).toBool();
    // Absent means true, matching `emitRequirements !== false`.
    installed.emitRequirements =
        object.value(QStringLiteral("emitRequirements")).toBool(true);
    installedFiles.append(installed);
  }

  QSet<QString> uninstalled;
  for (const QJsonValue& value :
       fixture.value(QStringLiteral("uninstalledFileVersionUids")).toArray()) {
    uninstalled.insert(value.toString());
  }

  QList<CandidateRow> allCandidates;
  for (const QJsonValue& value : fixture.value(QStringLiteral("candidates")).toArray()) {
    allCandidates.append(parseCandidateRow(value.toObject()));
  }

  QList<FileVersionDetail> allDetails;
  for (const QJsonValue& value :
       fixture.value(QStringLiteral("fileVersionDetails")).toArray()) {
    allDetails.append(parseDetail(value.toObject()));
  }

  QList<ModDetail> allMods;
  for (const QJsonValue& value : fixture.value(QStringLiteral("modDetails")).toArray()) {
    allMods.append(parseModDetail(value.toObject()));
  }

  const QJsonObject hydration = fixture.value(QStringLiteral("hydration")).toObject();
  for (auto it = hydration.constBegin(); it != hydration.constEnd(); ++it) {
    g_hydrationRaw.insert(it.key(),
                          it.value().toObject().value(QStringLiteral("file")).toObject());
  }

  // --- ports, filtering exactly as the runner's TS counterpart does ----------

  ResolverPorts ports;
  ports.fetchCandidates = [&](const QStringList& uids) {
    const QSet<QString> wanted(uids.begin(), uids.end());
    QList<CandidateRow> out;
    for (const CandidateRow& row : allCandidates) {
      if (wanted.contains(row.sourceFileVersionUid)) {
        out.append(row);
      }
    }
    return out;
  };
  ports.fetchFileVersionDetails = [&](const QStringList& uids) {
    const QSet<QString> wanted(uids.begin(), uids.end());
    QList<FileVersionDetail> out;
    for (const FileVersionDetail& detail : allDetails) {
      if (wanted.contains(detail.fileVersionUid)) {
        out.append(detail);
      }
    }
    return out;
  };
  ports.fetchModDetails = [&](const QStringList& uids) {
    const QSet<QString> wanted(uids.begin(), uids.end());
    QList<ModDetail> out;
    for (const ModDetail& mod : allMods) {
      if (wanted.contains(mod.modUid)) {
        out.append(mod);
      }
    }
    return out;
  };

  ResolverContext context;
  context.installedFiles             = installedFiles;
  context.uninstalledFileVersionUids = uninstalled;
  context.ports                      = ports;

  const FileRequirementsReport report = checkFileLevelRequirements(context);

  // --- hydration ------------------------------------------------------------

  const HydrateFile hydrate = [&](const QString& fileUID) -> std::optional<HydratedFile> {
    const QJsonObject entry = hydration.value(fileUID).toObject();
    if (entry.isEmpty()) {
      return std::nullopt;
    }
    const QJsonObject fileObject = entry.value(QStringLiteral("file")).toObject();

    HydratedFile hydrated;
    if (entry.value(QStringLiteral("kind")).toString() == QLatin1String("downloaded")) {
      hydrated.kind = HydratedFile::Kind::Downloaded;
      hydrated.downloaded.downloadId =
          fileObject.value(QStringLiteral("downloadId")).toString();
      hydrated.downloaded.fileUID = fileObject.value(QStringLiteral("fileUID")).toString();
      hydrated.downloaded.modUID  = fileObject.value(QStringLiteral("modUID")).toString();
      hydrated.downloaded.modName = fileObject.value(QStringLiteral("modName")).toString();
      hydrated.downloaded.modSummary =
          fileObject.value(QStringLiteral("modSummary")).toString();
      hydrated.downloaded.fileName =
          fileObject.value(QStringLiteral("fileName")).toString();
      hydrated.downloaded.version = fileObject.value(QStringLiteral("version")).toString();
      hydrated.downloaded.adultContent =
          fileObject.value(QStringLiteral("adultContent")).toBool();
      hydrated.downloaded.thumbnailUrl =
          fileObject.value(QStringLiteral("thumbnailUrl")).toString();
    } else {
      hydrated.kind             = HydratedFile::Kind::Installed;
      hydrated.installed.modId  = fileObject.value(QStringLiteral("modId")).toString();
      hydrated.installed.fileUID =
          fileObject.value(QStringLiteral("fileUID")).toString();
      hydrated.installed.modUID = fileObject.value(QStringLiteral("modUID")).toString();
      hydrated.installed.modName =
          fileObject.value(QStringLiteral("modName")).toString();
      hydrated.installed.thumbnailUrl =
          fileObject.value(QStringLiteral("thumbnailUrl")).toString();
      hydrated.installed.fileName =
          fileObject.value(QStringLiteral("fileName")).toString();
      hydrated.installed.version = fileObject.value(QStringLiteral("version")).toString();
      hydrated.installed.adultContent =
          fileObject.value(QStringLiteral("adultContent")).toBool();
      hydrated.installed.enabled = fileObject.value(QStringLiteral("enabled")).toBool();
    }
    return hydrated;
  };

  MapperContext mapperContext;
  mapperContext.gameId      = fixture.value(QStringLiteral("gameId")).toString();
  mapperContext.modsChecked = static_cast<int>(installedFiles.size());
  // Parity mode: the MO2-only suppression rule stays off so both
  // implementations classify the fixture identically.
  mapperContext.suppressMO2SelfRequirement = false;

  const FileRequirementsMetadata metadata =
      mapRequirementsReport(report, hydrate, mapperContext);

  // --- output ---------------------------------------------------------------

  QJsonObject out;
  out.insert(QStringLiteral("fixture"), fixture.value(QStringLiteral("name")).toString());
  out.insert(QStringLiteral("report"), reportToJson(report));
  out.insert(QStringLiteral("metadata"), metadataToJson(metadata));

  QTextStream stream(stdout);
  stream << QString::fromUtf8(QJsonDocument(out).toJson(QJsonDocument::Indented));

  return 0;
}
