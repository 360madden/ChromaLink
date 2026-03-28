-- script name: RIFT/Diagnostics.lua
-- version: 0.5.0
-- purpose: Provides lightweight logging and layout diagnostics for the ChromaLink player-target HUD integration.
-- dependencies: Core/Config.lua
-- important assumptions: Uses Command.Console.Display HTML mode when available and falls back to print-based logging otherwise.
-- protocol version: ChromaLink
-- framework module role: RIFT diagnostics
-- character count note: Character count not precomputed; measure with tooling if needed.

ChromaLink = ChromaLink or {}
ChromaLink.Diagnostics = {}
ChromaLink.Diagnostics.probeState = {
  lastSignature = nil,
  logCount = 0
}
ChromaLink.Diagnostics.validationState = {
  lastSignature = nil,
  logCount = 0
}

function ChromaLink.Diagnostics.Log(message)
  local formatted = "[ChromaLink] " .. tostring(message)

  if Command ~= nil and Command.Console ~= nil and Command.Console.Display ~= nil then
    local colorHex = ChromaLink.Config.chatColorHex or "#4DEAFF"
    local escaped = formatted
    escaped = string.gsub(escaped, "&", "&amp;")
    escaped = string.gsub(escaped, "<", "&lt;")
    escaped = string.gsub(escaped, ">", "&gt;")
    local htmlText = "<font color=\"" .. colorHex .. "\">" .. escaped .. "</font>"
    Command.Console.Display("general", true, htmlText, true)
    return
  end

  print(formatted)
end

function ChromaLink.Diagnostics.DescribeStrataList(frame)
  local list = {}
  local index = 1
  local source = frame:GetStrataList()
  local key
  local value

  if source == nil then
    return "(unavailable)"
  end

  for key, value in pairs(source) do
    if type(value) == "string" then
      list[index] = value
    else
      list[index] = tostring(key)
    end
    index = index + 1
  end

  if #list == 0 then
    return "(empty)"
  end

  return table.concat(list, ", ")
end

function ChromaLink.Diagnostics.LogLoaded()
  local config = ChromaLink.Config
  ChromaLink.Diagnostics.Log(config.addonIdentifier .. " v" .. config.addonVersion .. " loaded.")
end

function ChromaLink.Diagnostics.BoolText(value)
  if value then
    return "true"
  end

  return "false"
end

function ChromaLink.Diagnostics.TextOrFallback(value)
  if value == nil then
    return "(nil)"
  end

  local text = tostring(value)
  if text == "" then
    return "(empty)"
  end

  return text
end

function ChromaLink.Diagnostics.BuildProbeSignature(probe)
  return table.concat({
    ChromaLink.Diagnostics.TextOrFallback(probe.rawCalling),
    ChromaLink.Diagnostics.TextOrFallback(probe.rawRole),
    tostring(probe.normalizedCallingCode or 0),
    tostring(probe.normalizedRoleCode or 0),
    tostring(probe.selectedResourceKind or 0),
    ChromaLink.Diagnostics.TextOrFallback(probe.selectedResourceSource),
    tostring(probe.selectedResourceCurrent or 0),
    tostring(probe.selectedResourceMax or 0),
    ChromaLink.Diagnostics.TextOrFallback(probe.targetName),
    tostring(probe.targetCallingCode or 0),
    tostring(probe.targetResourceKind or 0),
    tostring(probe.targetResourceCurrent or 0),
    tostring(probe.targetResourceMax or 0),
    tostring(probe.powerAttack or 0),
    tostring(probe.critAttack or 0),
    tostring(probe.powerSpell or 0),
    tostring(probe.critSpell or 0),
    tostring(probe.critPower or 0),
    tostring(probe.hit or 0),
    ChromaLink.Diagnostics.BoolText(probe.castActive),
    tostring(probe.castFlags or 0),
    tostring(probe.castProgressQ15 or 0),
    ChromaLink.Diagnostics.TextOrFallback(probe.castAbilityName)
  }, "|")
end

function ChromaLink.Diagnostics.IsSuspiciousProbe(probe)
  local rawCalling = probe.rawCalling or ""
  local targetCalling = probe.targetCalling or ""

  if rawCalling ~= "" and probe.normalizedCallingCode == 0 then
    return true
  end

  if (probe.rawRole or "") ~= "" and probe.normalizedRoleCode == 0 then
    return true
  end

  if (rawCalling == "cleric" or rawCalling == "mage") and probe.selectedResourceKind ~= 1 then
    return true
  end

  if targetCalling ~= "" and (probe.targetCallingCode or 0) == 0 then
    return true
  end

  if ((probe.castAbilityName or "") ~= "" or (probe.castDurationSeconds or 0) > 0 or (probe.castRemainingSeconds or 0) > 0)
      and not probe.castActive then
    return true
  end

  return false
end

function ChromaLink.Diagnostics.LogGatherProbe(probe, reason)
  local config = ChromaLink.Config
  local state = ChromaLink.Diagnostics.probeState
  local maxEvents = config.probeLoggingMaxEvents or 0
  local signature
  local suspicious

  if not config.probeLoggingEnabled or probe == nil then
    return
  end

  signature = ChromaLink.Diagnostics.BuildProbeSignature(probe)
  suspicious = ChromaLink.Diagnostics.IsSuspiciousProbe(probe)

  if state.lastSignature == signature and not suspicious then
    return
  end

  if maxEvents > 0 and state.logCount >= maxEvents and not suspicious then
    state.lastSignature = signature
    return
  end

  state.lastSignature = signature
  state.logCount = state.logCount + 1

  ChromaLink.Diagnostics.Log(
    "Probe[" .. tostring(reason or "refresh") .. "] "
      .. "calling=" .. ChromaLink.Diagnostics.TextOrFallback(probe.rawCalling)
      .. " matchedCalling=" .. ChromaLink.Diagnostics.TextOrFallback(probe.matchedCalling)
      .. " callingCode=" .. tostring(probe.normalizedCallingCode or 0)
      .. " role=" .. ChromaLink.Diagnostics.TextOrFallback(probe.rawRole)
      .. " matchedRole=" .. ChromaLink.Diagnostics.TextOrFallback(probe.matchedRole)
      .. " roleCode=" .. tostring(probe.normalizedRoleCode or 0)
      .. " race=" .. ChromaLink.Diagnostics.TextOrFallback(probe.race)
      .. "/" .. ChromaLink.Diagnostics.TextOrFallback(probe.raceName)
      .. " mana=" .. tostring(probe.mana or "nil") .. "/" .. tostring(probe.manaMax or "nil")
      .. " energy=" .. tostring(probe.energy or "nil") .. "/" .. tostring(probe.energyMax or "nil")
      .. " charge=" .. tostring(probe.charge or "nil") .. "/" .. tostring(probe.chargeMax or "nil")
      .. " planar=" .. tostring(probe.planar or "nil") .. "/" .. tostring(probe.planarMax or "nil")
      .. " power=" .. tostring(probe.power or "nil")
      .. " selectedResource=" .. tostring(probe.selectedResourceKind or 0) .. ":" .. ChromaLink.Diagnostics.TextOrFallback(probe.selectedResourceSource)
      .. " selectedValue=" .. tostring(probe.selectedResourceCurrent or 0) .. "/" .. tostring(probe.selectedResourceMax or 0)
      .. " playerUnitId=" .. ChromaLink.Diagnostics.TextOrFallback(probe.playerUnitId)
      .. " target=" .. ChromaLink.Diagnostics.TextOrFallback(probe.targetName)
      .. " targetUnitId=" .. ChromaLink.Diagnostics.TextOrFallback(probe.targetUnitId)
      .. " targetCalling=" .. ChromaLink.Diagnostics.TextOrFallback(probe.targetCalling)
      .. " targetCallingCode=" .. tostring(probe.targetCallingCode or 0)
      .. " targetHealth=" .. tostring(probe.targetHealth or "nil") .. "/" .. tostring(probe.targetHealthMax or "nil")
      .. " targetResource=" .. tostring(probe.targetResourceKind or 0) .. ":" .. tostring(probe.targetResourceCurrent or 0) .. "/" .. tostring(probe.targetResourceMax or 0)
      .. " offense=" .. tostring(probe.powerAttack or 0) .. "/" .. tostring(probe.critAttack or 0) .. "/" .. tostring(probe.powerSpell or 0) .. "/" .. tostring(probe.critSpell or 0) .. "/" .. tostring(probe.critPower or 0) .. "/" .. tostring(probe.hit or 0)
      .. " cast=" .. ChromaLink.Diagnostics.TextOrFallback(probe.castAbilityName)
      .. " castSource=" .. ChromaLink.Diagnostics.TextOrFallback(probe.castSource)
      .. " active=" .. ChromaLink.Diagnostics.BoolText(probe.castActive)
      .. " flags=" .. tostring(probe.castFlags or 0)
      .. " remaining=" .. tostring(probe.castRemainingSeconds or 0)
      .. " duration=" .. tostring(probe.castDurationSeconds or 0)
      .. " progress=" .. tostring(probe.castProgressQ15 or 0)
  )
end

local function BuildValidationIssueText(issue)
  local field = issue.field or "snapshot"
  local code = issue.code or "issue"
  local message = issue.message or ""
  local value = issue.value

  return tostring(issue.severity or "info") .. ":" .. code .. ":" .. field .. "=" .. ChromaLink.Diagnostics.TextOrFallback(value) .. " " .. message
end

function ChromaLink.Diagnostics.BuildValidationSignature(report)
  local issues = report.issues or {}
  local maxIssues = ChromaLink.Config.validationSummaryMaxIssues or 0
  local parts = {
    tostring(report.valid and 1 or 0),
    tostring(report.errorCount or 0),
    tostring(report.warningCount or 0)
  }
  local index

  for index = 1, math.min(#issues, maxIssues) do
    local issue = issues[index]
    parts[#parts + 1] = tostring(issue.severity or "info")
      .. ":"
      .. tostring(issue.code or "issue")
      .. ":"
      .. tostring(issue.field or "snapshot")
  end

  return table.concat(parts, "|")
end

function ChromaLink.Diagnostics.LogSnapshotValidation(report, reason)
  local config = ChromaLink.Config
  local state = ChromaLink.Diagnostics.validationState
  local maxEvents = config.validationLogMaxEvents or 0
  local signature
  local issueText = {}
  local index

  if not config.validationLoggingEnabled or report == nil then
    return
  end

  if (report.issueCount or 0) <= 0 then
    return
  end

  signature = ChromaLink.Diagnostics.BuildValidationSignature(report)

  if state.lastSignature == signature then
    return
  end

  if maxEvents > 0 and state.logCount >= maxEvents then
    state.lastSignature = signature
    return
  end

  state.lastSignature = signature
  state.logCount = state.logCount + 1

  for index = 1, math.min(#(report.issues or {}), config.validationSummaryMaxIssues or 0) do
    issueText[#issueText + 1] = BuildValidationIssueText(report.issues[index])
  end

  ChromaLink.Diagnostics.Log(
    "Sanity[" .. tostring(reason or "refresh") .. "] "
      .. tostring(report.valid and "warn" or "fail")
      .. " " .. tostring(report.summary or "errors=0 warnings=0")
      .. " issues=" .. table.concat(issueText, " ; ")
  )
end

-- end-of-script marker comment
