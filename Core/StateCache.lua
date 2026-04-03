ChromaLink = ChromaLink or {}
ChromaLink.StateCache = ChromaLink.StateCache or {}

local addonIdentifier = ((ChromaLink.Config or {}).addonIdentifier) or "ChromaLink"
local cacheConfig = (ChromaLink.Config or {}).stateCache or {}

local state = {
  initialized = false,
  eventAttachmentsInstalled = false,
  units = {},
  unitAliases = {},
  buffs = {},
  abilityList = nil,
  abilityListRefreshedAt = 0,
  abilityDetails = {},
  dirty = {
    unitAll = true,
    castAll = true,
    abilityList = true,
    abilities = {},
    buffs = {}
  },
  counters = {
    unitInvalidations = 0,
    castInvalidations = 0,
    buffInvalidations = 0,
    abilityInvalidations = 0,
    unitRefreshes = 0,
    buffRefreshes = 0,
    abilityRefreshes = 0
  },
  lastInitAt = 0,
  lastPrimeAt = 0
}

ChromaLink.StateCache.state = state

local function SafeNow()
  if Inspect ~= nil and Inspect.Time ~= nil and Inspect.Time.Real ~= nil then
    local ok, result = pcall(Inspect.Time.Real)
    if ok then
      return tonumber(result) or 0
    end
  end

  return 0
end

local function SafeCall(fn, ...)
  if fn == nil then
    return nil
  end

  local ok, result = pcall(fn, ...)
  if ok then
    return result
  end

  return nil
end

local function NormalizeKey(value)
  if value == nil then
    return ""
  end

  return tostring(value)
end

local function FindFirstTable(...)
  local index

  for index = 1, select("#", ...) do
    local value = select(index, ...)
    if type(value) == "table" then
      return value
    end
  end

  return nil
end

local function FindAddonIdentifierArg(...)
  local index

  for index = 1, select("#", ...) do
    local value = select(index, ...)
    if type(value) == "string" and value == addonIdentifier then
      return value
    end
  end

  return nil
end

local function IsEnabled()
  return cacheConfig.enabled ~= false
end

local function ResolveUnitKey(unitReference, lookupResult)
  local normalizedLookup = NormalizeKey(lookupResult)
  if normalizedLookup ~= "" then
    return normalizedLookup
  end

  return NormalizeKey(unitReference)
end

local function ResolveTrackedSpecifiers()
  local specifiers = {
    "player",
    "player.target",
    "focus"
  }
  local dedupe = {
    ["player"] = true,
    ["player.target"] = true,
    ["focus"] = true
  }
  local followConfig = (ChromaLink.Config or {}).followUnit or {}
  local slots = followConfig.slots
  local index
  local slot

  if type(followConfig.specifier) == "string" and followConfig.specifier ~= "" and not dedupe[followConfig.specifier] then
    table.insert(specifiers, followConfig.specifier)
    dedupe[followConfig.specifier] = true
  end

  if type(slots) ~= "table" then
    slots = { tonumber(followConfig.slot) or 1 }
  end

  for index = 1, #slots do
    slot = math.max(1, math.min(20, math.floor(tonumber(slots[index]) or 0)))
    if slot > 0 then
      local specifier = string.format("group%02d", slot)
      if not dedupe[specifier] then
        table.insert(specifiers, specifier)
        dedupe[specifier] = true
      end
    end
  end

  return specifiers
end

local function AliasUnitRecord(specifier, unitId, key)
  if specifier ~= nil and specifier ~= "" then
    state.unitAliases[NormalizeKey(specifier)] = key
  end
  if unitId ~= nil and unitId ~= "" then
    state.unitAliases[NormalizeKey(unitId)] = key
  end
end

local function MarkUnitDirty(unitReference)
  local key = NormalizeKey(unitReference)
  if key ~= "" then
    state.dirty.buffs[key] = true
  end
  state.dirty.unitAll = true
  state.dirty.castAll = true
  state.counters.unitInvalidations = state.counters.unitInvalidations + 1
  state.counters.castInvalidations = state.counters.castInvalidations + 1
end

local function MarkBuffDirty(unitReference)
  local key = NormalizeKey(unitReference)
  if key ~= "" then
    state.dirty.buffs[key] = true
  else
    state.dirty.buffs["*"] = true
  end
  state.counters.buffInvalidations = state.counters.buffInvalidations + 1
end

local function MarkAbilityDirty(abilities, listChanged)
  if type(abilities) == "table" then
    local abilityId
    for abilityId in pairs(abilities) do
      state.dirty.abilities[NormalizeKey(abilityId)] = true
    end
  end

  if listChanged then
    state.dirty.abilityList = true
  end

  state.counters.abilityInvalidations = state.counters.abilityInvalidations + 1
end

local function ResolveExistingUnitRecord(unitReference)
  local key = NormalizeKey(unitReference)
  local aliasKey = state.unitAliases[key]

  if aliasKey ~= nil and state.units[aliasKey] ~= nil then
    return state.units[aliasKey]
  end

  return state.units[key]
end

local function RefreshUnitRecord(unitReference)
  local lookupResult
  local detail
  local castbar
  local key
  local record

  if not IsEnabled() then
    return nil
  end

  if type(unitReference) == "string" and unitReference ~= "" then
    lookupResult = SafeCall(Inspect ~= nil and Inspect.Unit and Inspect.Unit.Lookup or nil, unitReference)
  end

  detail = SafeCall(Inspect ~= nil and Inspect.Unit and Inspect.Unit.Detail or nil, unitReference)
  castbar = SafeCall(Inspect ~= nil and Inspect.Unit and Inspect.Unit.Castbar or nil, unitReference)
  key = ResolveUnitKey(unitReference, lookupResult or (detail and detail.id))

  if key == "" then
    key = NormalizeKey(unitReference)
  end

  record = state.units[key] or {}
  record.key = key
  record.specifier = type(unitReference) == "string" and unitReference or record.specifier
  record.unitId = lookupResult or (detail and detail.id) or record.unitId
  record.detail = detail
  record.castbar = castbar
  record.refreshedAt = SafeNow()
  record.unitVersionSeen = state.counters.unitInvalidations
  record.castVersionSeen = state.counters.castInvalidations
  state.units[key] = record

  AliasUnitRecord(record.specifier, record.unitId, key)
  state.counters.unitRefreshes = state.counters.unitRefreshes + 1

  return record
end

local function UnitRecordNeedsRefresh(unitReference, record, wantsCast)
  local ttl = tonumber(wantsCast and cacheConfig.castTtlSeconds or cacheConfig.unitTtlSeconds) or 0
  local now = SafeNow()

  if record == nil then
    return true
  end
  if (record.unitVersionSeen or -1) ~= state.counters.unitInvalidations then
    return true
  end
  if wantsCast and (record.castVersionSeen or -1) ~= state.counters.castInvalidations then
    return true
  end
  if wantsCast and record.castbar == nil then
    return true
  end
  if ttl > 0 and now > 0 and (record.refreshedAt == nil or (now - record.refreshedAt) > ttl) then
    return true
  end

  return false
end

local function EnsureUnitRecord(unitReference, wantsCast)
  local record = ResolveExistingUnitRecord(unitReference)

  if UnitRecordNeedsRefresh(unitReference, record, wantsCast) then
    record = RefreshUnitRecord(unitReference)
  end
  return record
end

local function ResolveBuffRecordKey(unitReference)
  local existing = ResolveExistingUnitRecord(unitReference)
  if existing ~= nil then
    return existing.key
  end
  return NormalizeKey(unitReference)
end

local function RefreshBuffRecord(unitReference)
  local record = EnsureUnitRecord(unitReference, false)
  local unitForBuffs = unitReference
  local buffIds
  local details
  local key

  if record ~= nil and record.unitId ~= nil then
    unitForBuffs = record.unitId
  end

  buffIds = SafeCall(Inspect ~= nil and Inspect.Buff and Inspect.Buff.List or nil, unitForBuffs)
  details = SafeCall(Inspect ~= nil and Inspect.Buff and Inspect.Buff.Detail or nil, unitForBuffs, buffIds)
  key = ResolveBuffRecordKey(unitReference)

  state.buffs[key] = {
    buffIds = buffIds,
    details = details,
    refreshedAt = SafeNow(),
    unitId = record and record.unitId or nil,
    specifier = record and record.specifier or (type(unitReference) == "string" and unitReference or nil)
  }
  state.dirty.buffs[key] = nil
  state.dirty.buffs[NormalizeKey(unitReference)] = nil
  state.dirty.buffs["*"] = nil
  state.counters.buffRefreshes = state.counters.buffRefreshes + 1

  return state.buffs[key]
end

local function EnsureBuffRecord(unitReference)
  local ttl = tonumber(cacheConfig.buffTtlSeconds) or 0
  local now = SafeNow()
  local key = ResolveBuffRecordKey(unitReference)
  local record = state.buffs[key]

  if record == nil
    or state.dirty.buffs["*"]
    or state.dirty.buffs[key]
    or state.dirty.buffs[NormalizeKey(unitReference)]
    or (ttl > 0 and now > 0 and (record.refreshedAt == nil or (now - record.refreshedAt) > ttl)) then
    record = RefreshBuffRecord(unitReference)
  end

  return record
end

local function RefreshAbilityList()
  state.abilityList = SafeCall(Inspect ~= nil and Inspect.Ability and Inspect.Ability.New and Inspect.Ability.New.List or nil) or {}
  state.abilityListRefreshedAt = SafeNow()
  state.dirty.abilityList = false
  return state.abilityList
end

local function EnsureAbilityList()
  local ttl = tonumber(cacheConfig.abilityTtlSeconds) or 0
  local now = SafeNow()

  if state.abilityList == nil
    or state.dirty.abilityList
    or (ttl > 0 and now > 0 and (state.abilityListRefreshedAt == nil or (now - state.abilityListRefreshedAt) > ttl)) then
    return RefreshAbilityList()
  end

  return state.abilityList
end

local function RefreshAbilityDetail(abilityReference)
  local key = NormalizeKey(abilityReference)
  local detail = SafeCall(Inspect ~= nil and Inspect.Ability and Inspect.Ability.New and Inspect.Ability.New.Detail or nil, abilityReference)

  state.abilityDetails[key] = {
    detail = detail,
    refreshedAt = SafeNow()
  }
  state.dirty.abilities[key] = nil
  state.counters.abilityRefreshes = state.counters.abilityRefreshes + 1

  return detail
end

local function EnsureAbilityDetail(abilityReference)
  local key = NormalizeKey(abilityReference)
  local ttl = tonumber(cacheConfig.abilityTtlSeconds) or 0
  local now = SafeNow()
  local record = state.abilityDetails[key]

  if record == nil
    or state.dirty.abilities[key]
    or (ttl > 0 and now > 0 and (record.refreshedAt == nil or (now - record.refreshedAt) > ttl)) then
    return RefreshAbilityDetail(abilityReference)
  end

  return record.detail
end

local function PrimeTrackedUnits()
  local tracked = ResolveTrackedSpecifiers()
  local _, specifier

  for _, specifier in ipairs(tracked) do
    RefreshUnitRecord(specifier)
    RefreshBuffRecord(specifier)
  end

  state.lastPrimeAt = SafeNow()
end

local function AttachEvent(eventRef, handler, label)
  if eventRef == nil or Command == nil or Command.Event == nil or Command.Event.Attach == nil then
    return false
  end

  return pcall(Command.Event.Attach, eventRef, handler, label)
end

local function ResolveEvent(parts)
  local current = Event
  local _, part

  for _, part in ipairs(parts) do
    if current == nil then
      return nil
    end
    current = current[part]
  end

  return current
end

local function AttachNamedEvent(parts, handler, suffix)
  local eventRef = ResolveEvent(parts)
  if eventRef == nil then
    return false
  end

  return AttachEvent(eventRef, handler, "ChromaLink.StateCache." .. suffix)
end

local function InstallEventAttachments()
  if state.eventAttachmentsInstalled then
    return
  end

  AttachNamedEvent({ "Addon", "Load", "End" }, function(...)
    if FindAddonIdentifierArg(...) ~= addonIdentifier then
      return
    end

    state.initialized = true
    state.lastInitAt = SafeNow()
    state.dirty.unitAll = true
    state.dirty.castAll = true
    state.dirty.abilityList = true

    if cacheConfig.primeTrackedUnitsOnInit ~= false then
      PrimeTrackedUnits()
      EnsureAbilityList()
    end
  end, "Addon.Load.End")

  local abilityDirtyOnly = function(...)
    MarkAbilityDirty(FindFirstTable(...), false)
  end
  local abilityDirtyAndList = function(...)
    MarkAbilityDirty(FindFirstTable(...), true)
  end

  AttachNamedEvent({ "Ability", "New", "Add" }, abilityDirtyAndList, "Ability.New.Add")
  AttachNamedEvent({ "Ability", "New", "Remove" }, abilityDirtyAndList, "Ability.New.Remove")
  AttachNamedEvent({ "Ability", "New", "Cooldown", "Begin" }, abilityDirtyOnly, "Ability.New.Cooldown.Begin")
  AttachNamedEvent({ "Ability", "New", "Cooldown", "End" }, abilityDirtyOnly, "Ability.New.Cooldown.End")
  AttachNamedEvent({ "Ability", "New", "Range", "True" }, abilityDirtyOnly, "Ability.New.Range.True")
  AttachNamedEvent({ "Ability", "New", "Range", "False" }, abilityDirtyOnly, "Ability.New.Range.False")
  AttachNamedEvent({ "Ability", "New", "Usable", "True" }, abilityDirtyOnly, "Ability.New.Usable.True")
  AttachNamedEvent({ "Ability", "New", "Usable", "False" }, abilityDirtyOnly, "Ability.New.Usable.False")
  AttachNamedEvent({ "Ability", "New", "Target" }, abilityDirtyOnly, "Ability.New.Target")

  local unitDirty = function()
    MarkUnitDirty("*")
  end
  local buffDirty = function()
    MarkBuffDirty("*")
  end
  local castDirty = function()
    state.dirty.castAll = true
    state.counters.castInvalidations = state.counters.castInvalidations + 1
  end

  AttachNamedEvent({ "Unit", "Add" }, unitDirty, "Unit.Add")
  AttachNamedEvent({ "Unit", "Remove" }, unitDirty, "Unit.Remove")
  AttachNamedEvent({ "Unit", "Availability", "Full" }, unitDirty, "Unit.Availability.Full")
  AttachNamedEvent({ "Unit", "Availability", "Partial" }, unitDirty, "Unit.Availability.Partial")
  AttachNamedEvent({ "Unit", "Availability", "None" }, unitDirty, "Unit.Availability.None")
  AttachNamedEvent({ "Unit", "Detail", "Combat" }, unitDirty, "Unit.Detail.Combat")
  AttachNamedEvent({ "Unit", "Detail", "Health" }, unitDirty, "Unit.Detail.Health")
  AttachNamedEvent({ "Unit", "Detail", "HealthMax" }, unitDirty, "Unit.Detail.HealthMax")
  AttachNamedEvent({ "Unit", "Detail", "Mana" }, unitDirty, "Unit.Detail.Mana")
  AttachNamedEvent({ "Unit", "Detail", "ManaMax" }, unitDirty, "Unit.Detail.ManaMax")
  AttachNamedEvent({ "Unit", "Detail", "Energy" }, unitDirty, "Unit.Detail.Energy")
  AttachNamedEvent({ "Unit", "Detail", "EnergyMax" }, unitDirty, "Unit.Detail.EnergyMax")
  AttachNamedEvent({ "Unit", "Detail", "Power" }, unitDirty, "Unit.Detail.Power")
  AttachNamedEvent({ "Unit", "Detail", "Combo" }, unitDirty, "Unit.Detail.Combo")
  AttachNamedEvent({ "Unit", "Detail", "Charge" }, unitDirty, "Unit.Detail.Charge")
  AttachNamedEvent({ "Unit", "Detail", "ChargeMax" }, unitDirty, "Unit.Detail.ChargeMax")
  AttachNamedEvent({ "Unit", "Detail", "Planar" }, unitDirty, "Unit.Detail.Planar")
  AttachNamedEvent({ "Unit", "Detail", "PlanarMax" }, unitDirty, "Unit.Detail.PlanarMax")
  AttachNamedEvent({ "Unit", "Detail", "Absorb" }, unitDirty, "Unit.Detail.Absorb")
  AttachNamedEvent({ "Unit", "Detail", "Ready" }, unitDirty, "Unit.Detail.Ready")
  AttachNamedEvent({ "Unit", "Detail", "Afk" }, unitDirty, "Unit.Detail.Afk")
  AttachNamedEvent({ "Unit", "Detail", "Level" }, unitDirty, "Unit.Detail.Level")
  AttachNamedEvent({ "Unit", "Detail", "Role" }, unitDirty, "Unit.Detail.Role")
  AttachNamedEvent({ "Unit", "Detail", "Tagged" }, unitDirty, "Unit.Detail.Tagged")
  AttachNamedEvent({ "Unit", "Detail", "Mark" }, unitDirty, "Unit.Detail.Mark")
  AttachNamedEvent({ "Unit", "Detail", "Coord" }, unitDirty, "Unit.Detail.Coord")
  AttachNamedEvent({ "Unit", "Castbar" }, castDirty, "Unit.Castbar")

  AttachNamedEvent({ "Buff", "Add" }, buffDirty, "Buff.Add")
  AttachNamedEvent({ "Buff", "Change" }, buffDirty, "Buff.Change")
  AttachNamedEvent({ "Buff", "Description" }, buffDirty, "Buff.Description")
  AttachNamedEvent({ "Buff", "Remove" }, buffDirty, "Buff.Remove")

  state.eventAttachmentsInstalled = true
end

function ChromaLink.StateCache.Initialize()
  if not IsEnabled() then
    return
  end

  InstallEventAttachments()
  state.initialized = true
  state.lastInitAt = SafeNow()

  if cacheConfig.primeTrackedUnitsOnInit ~= false then
    PrimeTrackedUnits()
    EnsureAbilityList()
  end
end

function ChromaLink.StateCache.ForceRefresh()
  if not IsEnabled() then
    return
  end

  state.dirty.unitAll = true
  state.dirty.castAll = true
  state.dirty.abilityList = true
  state.dirty.buffs["*"] = true
  PrimeTrackedUnits()
  EnsureAbilityList()
end

function ChromaLink.StateCache.GetUnitId(unitReference)
  if not IsEnabled() then
    return nil
  end

  local record = EnsureUnitRecord(unitReference, false)
  return record and record.unitId or nil
end

function ChromaLink.StateCache.GetUnitDetail(unitReference)
  if not IsEnabled() then
    return nil
  end

  local record = EnsureUnitRecord(unitReference, false)
  return record and record.detail or nil
end

function ChromaLink.StateCache.GetUnitCastbar(unitReference)
  if not IsEnabled() then
    return nil
  end

  local record = EnsureUnitRecord(unitReference, true)
  return record and record.castbar or nil
end

function ChromaLink.StateCache.GetAbilityList()
  if not IsEnabled() then
    return nil
  end

  return EnsureAbilityList()
end

function ChromaLink.StateCache.GetAbilityDetail(abilityReference)
  if not IsEnabled() then
    return nil
  end

  if type(abilityReference) == "table" then
    return SafeCall(Inspect ~= nil and Inspect.Ability and Inspect.Ability.New and Inspect.Ability.New.Detail or nil, abilityReference)
  end

  if abilityReference == nil or abilityReference == "" then
    return nil
  end

  return EnsureAbilityDetail(abilityReference)
end

function ChromaLink.StateCache.GetBuffList(unitReference)
  if not IsEnabled() then
    return nil
  end

  local record = EnsureBuffRecord(unitReference)
  return record and record.buffIds or nil
end

function ChromaLink.StateCache.GetBuffDetail(unitReference, _)
  if not IsEnabled() then
    return nil
  end

  local record = EnsureBuffRecord(unitReference)
  return record and record.details or nil
end

function ChromaLink.StateCache.GetStatus()
  local dirtyBuffCount = 0
  local dirtyAbilityCount = 0
  local unitCount = 0
  local _

  for _ in pairs(state.dirty.buffs) do
    dirtyBuffCount = dirtyBuffCount + 1
  end
  for _ in pairs(state.dirty.abilities) do
    dirtyAbilityCount = dirtyAbilityCount + 1
  end
  for _ in pairs(state.units) do
    unitCount = unitCount + 1
  end

  return {
    initialized = state.initialized,
    lastInitAt = state.lastInitAt,
    lastPrimeAt = state.lastPrimeAt,
    trackedUnitRecords = unitCount,
    dirtyAbilityCount = dirtyAbilityCount,
    dirtyBuffCount = dirtyBuffCount,
    counters = state.counters
  }
end

function ChromaLink.StateCache.LogStatus()
  local status = ChromaLink.StateCache.GetStatus()

  if ChromaLink.Diagnostics == nil or ChromaLink.Diagnostics.Log == nil then
    return
  end

  ChromaLink.Diagnostics.Log(string.format(
    "State cache: enabled=%s initialized=%s units=%d dirtyAbilities=%d dirtyBuffs=%d unitRefreshes=%d abilityRefreshes=%d buffRefreshes=%d.",
    IsEnabled() and "on" or "off",
    status.initialized and "yes" or "no",
    tonumber(status.trackedUnitRecords) or 0,
    tonumber(status.dirtyAbilityCount) or 0,
    tonumber(status.dirtyBuffCount) or 0,
    tonumber((status.counters or {}).unitRefreshes) or 0,
    tonumber((status.counters or {}).abilityRefreshes) or 0,
    tonumber((status.counters or {}).buffRefreshes) or 0))
end

InstallEventAttachments()
