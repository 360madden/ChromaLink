ChromaLink = ChromaLink or {}
ChromaLink.AbilityExport = ChromaLink.AbilityExport or {}
ChromaLink_AbilityExport = ChromaLink_AbilityExport or {}

local addonIdentifier = (ChromaLink.Config and ChromaLink.Config.addonIdentifier) or "ChromaLink"
local addonVersion = (ChromaLink.Config and ChromaLink.Config.addonVersion) or "0.1.0"

local costFields = {
  { key = "costMana", label = "mana" },
  { key = "costPower", label = "power" },
  { key = "costEnergy", label = "energy" },
  { key = "costCharge", label = "charge" },
  { key = "costSpirit", label = "spirit" },
  { key = "costPlanarCharge", label = "planar" }
}

local state = {
  savedVariablesReady = false,
  pendingReason = nil,
  nextAttemptAt = 0,
  lastSnapshot = nil,
  exportCount = tonumber((type(ChromaLink_AbilityExport) == "table" and ChromaLink_AbilityExport.exportCount) or 0) or 0
}

ChromaLink.AbilityExport.state = state
ChromaLink.AbilityExport.saved = ChromaLink_AbilityExport

local function GetRealtimeNow()
  if Inspect ~= nil and Inspect.Time ~= nil and Inspect.Time.Real ~= nil then
    local ok, value = pcall(Inspect.Time.Real)
    if ok and value ~= nil then
      return tonumber(value) or 0
    end
  end

  return 0
end

local function NormalizeText(value, maxLength)
  local text = tostring(value or "")
  text = string.gsub(text, "%s+", " ")
  text = string.gsub(text, "^%s+", "")
  text = string.gsub(text, "%s+$", "")

  if maxLength ~= nil and maxLength > 0 and string.len(text) > maxLength then
    text = string.sub(text, 1, maxLength)
  end

  return text
end

local function NormalizeNumber(value, fallback)
  local number = tonumber(value)
  if number == nil then
    return fallback or 0
  end

  return number
end

local function NormalizeBoolean(value)
  return value and true or false
end

local function NormalizeScalar(value, maxLength)
  if value == nil then
    return ""
  end

  if type(value) == "number" then
    return value
  end

  return NormalizeText(value, maxLength)
end

local function SafePlayerDetail()
  if Inspect == nil or Inspect.Unit == nil or Inspect.Unit.Detail == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Unit.Detail, "player")
  if ok then
    return result
  end

  return nil
end

local function SafeAbilityList()
  if Inspect == nil or Inspect.Ability == nil or Inspect.Ability.New == nil or Inspect.Ability.New.List == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Ability.New.List)
  if ok then
    return result
  end

  return nil
end

local function SafeAbilityDetails(abilities)
  if abilities == nil or Inspect == nil or Inspect.Ability == nil or Inspect.Ability.New == nil or Inspect.Ability.New.Detail == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Ability.New.Detail, abilities)
  if ok then
    return result
  end

  return nil
end

local function SafeTemporaryRole()
  if Inspect == nil or Inspect.TEMPORARY == nil or Inspect.TEMPORARY.Role == nil then
    return nil
  end

  local ok, result = pcall(Inspect.TEMPORARY.Role)
  if ok then
    return result
  end

  return nil
end

local function ResolvePrimaryCost(detail)
  local _, field
  for _, field in ipairs(costFields) do
    local value = NormalizeNumber(detail and detail[field.key], 0)
    if value > 0 then
      return field.label, value
    end
  end

  return "none", 0
end

local function IsOffensiveCandidate(detail, primaryCostValue)
  local range = NormalizeNumber(detail and detail.rangeMax, 0)
  local weapon = string.lower(tostring((detail and detail.weapon) or ""))
  local weaponOnly = weapon == "melee" or weapon == "ranged"
  local activeWeaponOnly = weaponOnly and (
    NormalizeNumber(detail and detail.cooldown, 0) > 0
    or NormalizeNumber(detail and detail.castingTime, 0) > 0
    or NormalizeBoolean(detail and detail.channeled)
    or NormalizeBoolean(detail and detail.autoattack))
  local hasOffensiveShape = range > 0 or activeWeaponOnly

  return (not NormalizeBoolean(detail and detail.passive))
    and (not NormalizeBoolean(detail and detail.continuous))
    and primaryCostValue > 0
    and hasOffensiveShape
end

local function BuildAbilityRecord(detail)
  local primaryCostType, primaryCostValue = ResolvePrimaryCost(detail)
  local record = {
    id = tostring((detail and detail.id) or ""),
    idNew = tostring((detail and detail.idNew) or ""),
    name = NormalizeText(detail and detail.name, 120),
    description = NormalizeText(detail and detail.description, 280),
    castSeconds = NormalizeNumber(detail and detail.castingTime, 0),
    cooldownSeconds = NormalizeNumber(detail and detail.cooldown, 0),
    cooldownRemainingSeconds = NormalizeNumber(detail and detail.currentCooldownRemaining, 0),
    cooldownPaused = NormalizeBoolean(detail and detail.currentCooldownPaused),
    rangeMin = NormalizeNumber(detail and detail.rangeMin, 0),
    rangeMax = NormalizeNumber(detail and detail.rangeMax, 0),
    weapon = NormalizeText(detail and detail.weapon, 40),
    passive = NormalizeBoolean(detail and detail.passive),
    continuous = NormalizeBoolean(detail and detail.continuous),
    channeled = NormalizeBoolean(detail and detail.channeled),
    autoattack = NormalizeBoolean(detail and detail.autoattack),
    unusable = NormalizeBoolean(detail and detail.unusable),
    outOfRange = NormalizeBoolean(detail and detail.outOfRange),
    stealthRequired = NormalizeBoolean(detail and detail.stealthRequired),
    positioned = NormalizeBoolean(detail and detail.positioned),
    primaryCostType = primaryCostType,
    primaryCostValue = primaryCostValue,
    costMana = NormalizeNumber(detail and detail.costMana, 0),
    costPower = NormalizeNumber(detail and detail.costPower, 0),
    costEnergy = NormalizeNumber(detail and detail.costEnergy, 0),
    costCharge = NormalizeNumber(detail and detail.costCharge, 0),
    costSpirit = NormalizeNumber(detail and detail.costSpirit, 0),
    costPlanarCharge = NormalizeNumber(detail and detail.costPlanarCharge, 0)
  }

  record.isActive = (not record.passive) and (not record.continuous)
  record.isOffensiveCandidate = IsOffensiveCandidate(detail, primaryCostValue)
  return record
end

local function SortAbilityRecords(left, right)
  local leftName = string.lower(tostring(left and left.name or ""))
  local rightName = string.lower(tostring(right and right.name or ""))

  if leftName == rightName then
    local leftKey = tostring((left and left.idNew) or (left and left.id) or "")
    local rightKey = tostring((right and right.idNew) or (right and right.id) or "")
    return leftKey < rightKey
  end

  return leftName < rightName
end

local function BuildRawExport(records)
  local rows = {
    "name|cast|cd|range|weapon|costType|costValue|idNew|id"
  }

  local _, record
  for _, record in ipairs(records or {}) do
    rows[#rows + 1] = table.concat({
      record.name or "",
      string.format("%.2f", NormalizeNumber(record.castSeconds, 0)),
      string.format("%.2f", NormalizeNumber(record.cooldownSeconds, 0)),
      string.format("%.0f", NormalizeNumber(record.rangeMax, 0)),
      record.weapon or "",
      record.primaryCostType or "none",
      tostring(NormalizeNumber(record.primaryCostValue, 0)),
      record.idNew or "",
      record.id or ""
    }, "|")
  end

  rows[#rows + 1] = string.format("-- %d abilities", #records)
  return table.concat(rows, "\n")
end

local function BuildSnapshot(reason)
  local abilities = {}
  local offensive = {}
  local details = SafeAbilityDetails(SafeAbilityList())
  local player = SafePlayerDetail() or {}

  if type(details) == "table" then
    local _, detail
    for _, detail in pairs(details) do
      local record = BuildAbilityRecord(detail)
      table.insert(abilities, record)
      if record.isOffensiveCandidate then
        table.insert(offensive, record)
      end
    end
  end

  table.sort(abilities, SortAbilityRecords)
  table.sort(offensive, SortAbilityRecords)

  local activeCount = 0
  local _, ability
  for _, ability in ipairs(abilities) do
    if ability.isActive then
      activeCount = activeCount + 1
    end
  end

  return {
    schemaVersion = 1,
    addonIdentifier = addonIdentifier,
    addonVersion = addonVersion,
    status = (#abilities > 0) and "ready" or "pending",
    exportReason = tostring(reason or "unspecified"),
    exportCount = state.exportCount,
    generatedAtRealtime = GetRealtimeNow(),
    player = {
      name = NormalizeText(player.name, 64),
      level = NormalizeNumber(player.level, 0),
      calling = NormalizeScalar(player.calling, 32),
      role = NormalizeScalar(player.role or SafeTemporaryRole(), 32)
    },
    counts = {
      total = #abilities,
      active = activeCount,
      offensive = #offensive
    },
    abilities = abilities,
    offensiveAbilities = offensive,
    rawText = BuildRawExport(abilities),
    offensiveRawText = BuildRawExport(offensive)
  }
end

local function CommitSnapshot(snapshot)
  if snapshot == nil then
    return
  end

  ChromaLink_AbilityExport = snapshot
  ChromaLink.AbilityExport.saved = ChromaLink_AbilityExport
  state.lastSnapshot = snapshot
end

local function TryExport(reason, shouldLog)
  local snapshot = BuildSnapshot(reason)

  if snapshot.status == "ready" then
    state.exportCount = state.exportCount + 1
    snapshot.exportCount = state.exportCount
    state.pendingReason = nil
  else
    snapshot.exportCount = state.exportCount
    state.pendingReason = tostring(reason or state.pendingReason or "pending")
  end

  CommitSnapshot(snapshot)

  if shouldLog and ChromaLink.Diagnostics ~= nil and ChromaLink.Diagnostics.Log ~= nil then
    if snapshot.status == "ready" then
      ChromaLink.Diagnostics.Log(string.format(
        "Ability export refreshed: total=%d active=%d offensive=%d.",
        snapshot.counts.total or 0,
        snapshot.counts.active or 0,
        snapshot.counts.offensive or 0))
    else
      ChromaLink.Diagnostics.Log("Ability export is pending; live ability details are not ready yet.")
    end
  end

  return snapshot.status == "ready"
end

local function QueueExport(reason)
  state.pendingReason = tostring(reason or "pending")
  state.nextAttemptAt = 0
end

local function MaybeRetryPendingExport()
  if not state.savedVariablesReady or state.pendingReason == nil or state.pendingReason == "" then
    return
  end

  local now = GetRealtimeNow()
  if now < (state.nextAttemptAt or 0) then
    return
  end

  state.nextAttemptAt = now + 1
  TryExport(state.pendingReason, false)
end

function ChromaLink.AbilityExport.RequestExport(reason, shouldLog)
  if not state.savedVariablesReady then
    QueueExport(reason or "requested")
    return false
  end

  return TryExport(reason or "requested", shouldLog and true or false)
end

function ChromaLink.AbilityExport.GetSnapshot()
  return state.lastSnapshot or ChromaLink_AbilityExport
end

function ChromaLink.AbilityExport.LogStatus()
  local snapshot = ChromaLink.AbilityExport.GetSnapshot() or {}
  local counts = snapshot.counts or {}
  local player = snapshot.player or {}

  if ChromaLink.Diagnostics == nil or ChromaLink.Diagnostics.Log == nil then
    return
  end

  ChromaLink.Diagnostics.Log(string.format(
    "Ability export status=%s exports=%d player=%s level=%d calling=%s role=%s total=%d active=%d offensive=%d reason=%s.",
    tostring(snapshot.status or "missing"),
    tonumber(snapshot.exportCount or 0) or 0,
    tostring(player.name or "?"),
    tonumber(player.level or 0) or 0,
    tostring(player.calling or "?"),
    tostring(player.role or "?"),
    tonumber(counts.total or 0) or 0,
    tonumber(counts.active or 0) or 0,
    tonumber(counts.offensive or 0) or 0,
    tostring(snapshot.exportReason or state.pendingReason or "unknown")))
end

Command.Event.Attach(
  Event.Addon.SavedVariables.Load.End,
  function(_, loadedAddonIdentifier)
    if loadedAddonIdentifier ~= addonIdentifier then
      return
    end

    if type(ChromaLink_AbilityExport) ~= "table" then
      ChromaLink_AbilityExport = {}
    end

    state.savedVariablesReady = true
    state.lastSnapshot = ChromaLink_AbilityExport
    state.exportCount = tonumber(ChromaLink_AbilityExport.exportCount) or 0
    ChromaLink.AbilityExport.saved = ChromaLink_AbilityExport
    QueueExport("load-end")
    TryExport("load-end", true)
  end,
  "ChromaLink.AbilityExport.SavedVariables.Load.End")

Command.Event.Attach(
  Event.Addon.SavedVariables.Save.Begin,
  function(_, savingAddonIdentifier)
    if savingAddonIdentifier ~= addonIdentifier then
      return
    end

    TryExport("save-begin", false)
  end,
  "ChromaLink.AbilityExport.SavedVariables.Save.Begin")

Command.Event.Attach(
  Event.Ability.New.Add,
  function()
    if not state.savedVariablesReady then
      return
    end

    QueueExport("ability-add")
    TryExport("ability-add", false)
  end,
  "ChromaLink.AbilityExport.Ability.New.Add")

if Event ~= nil and Event.TEMPORARY ~= nil and Event.TEMPORARY.Role ~= nil then
  Command.Event.Attach(
    Event.TEMPORARY.Role,
    function()
      if not state.savedVariablesReady then
        return
      end

      QueueExport("role-change")
      TryExport("role-change", true)
    end,
    "ChromaLink.AbilityExport.TEMPORARY.Role")
end

Command.Event.Attach(
  Event.System.Update.End,
  MaybeRetryPendingExport,
  "ChromaLink.AbilityExport.System.Update.End")
