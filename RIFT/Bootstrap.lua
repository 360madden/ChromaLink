ChromaLink = ChromaLink or {}
ChromaLink.Bootstrap = {}

local addonIdentifier = ChromaLink.Config.addonIdentifier

local function ResolveStripCount()
  return math.max(1, math.floor(tonumber((ChromaLink.Config.profile or {}).stripCount) or 1))
end

local function GetRealtimeNow()
  if Inspect ~= nil and Inspect.Time ~= nil and Inspect.Time.Real ~= nil then
    return Inspect.Time.Real()
  end
  return 0
end

local function ResolveNativeTopLevel(frame)
  local current = frame
  local seen = {}

  while current ~= nil and current.GetParent ~= nil do
    if seen[current] then
      break
    end
    seen[current] = true

    local ok, parent = pcall(function()
      return current:GetParent()
    end)

    if not ok or parent == nil or parent == current then
      break
    end

    current = parent
  end

  return current
end

local function ResolveLayoutAnchor()
  local diagnosticsConfig = ChromaLink.Config.layoutDiagnostics
  if diagnosticsConfig == nil or not diagnosticsConfig.anchorContextToNativeTopLevel then
    return nil, "context-default"
  end

  local probeFrames = {
    UI.Native.PortraitPlayer,
    UI.Native.MapMini,
    UI.Native.Menu,
    UI.Native.Rift
  }

  local frame
  for _, frame in ipairs(probeFrames) do
    if frame ~= nil then
      local topLevel = ResolveNativeTopLevel(frame)
      if topLevel ~= nil then
        local topLevelName = topLevel
        if topLevel.GetName ~= nil then
          topLevelName = topLevel:GetName() or topLevel
        end
        return topLevel, "native-top-level:" .. tostring(topLevelName)
      end
    end
  end

  return nil, "context-default"
end

local function ResolveRequestedStrata(layout)
  local requested = ChromaLink.Config.requestedStrata
  if layout == nil or layout.GetStrataList == nil then
    return requested, nil
  end

  local ok, stratas = pcall(function()
    return layout:GetStrataList()
  end)

  if not ok or type(stratas) ~= "table" or #stratas == 0 then
    return requested, nil
  end

  if ChromaLink.Config.preferHighestAvailableStrata then
    return stratas[#stratas], stratas
  end

  local _, strata
  for _, strata in ipairs(stratas) do
    if strata == requested then
      return requested, stratas
    end
  end

  return requested, stratas
end

local function InstallLayoutDiagnostics(state)
  local diagnosticsConfig = ChromaLink.Config.layoutDiagnostics
  if diagnosticsConfig == nil or not diagnosticsConfig.enabled or not diagnosticsConfig.logEvents then
    return
  end

  ChromaLink.Diagnostics.AttachLayoutTrace(state.context, "layout.context")
  ChromaLink.Diagnostics.AttachLayoutTrace(state.root, "layout.root")
  if state.render.quietZone ~= nil then
    ChromaLink.Diagnostics.AttachLayoutTrace(state.render.quietZone, "layout.quietZone")
  end
  ChromaLink.Diagnostics.AttachLayoutTrace(state.render.band, "layout.band")

  if state.render.observerLane ~= nil then
    ChromaLink.Diagnostics.AttachLayoutTrace(state.render.observerLane.bar, "layout.observerLane")
  end

  local nativeFrames = {
    { label = "native.portraitPlayer", frame = UI.Native.PortraitPlayer },
    { label = "native.questStickies", frame = UI.Native.QuestStickies },
    { label = "native.mapMini", frame = UI.Native.MapMini },
    { label = "native.menu", frame = UI.Native.Menu },
    { label = "native.riftMeter", frame = UI.Native.Rift }
  }

  local entry
  for _, entry in ipairs(nativeFrames) do
    if entry.frame ~= nil then
      ChromaLink.Diagnostics.AttachLayoutTrace(entry.frame, entry.label)
    else
      ChromaLink.Diagnostics.Log(entry.label .. " [missing] frame unavailable.")
    end
  end
end

local function LogFrameStatus(label, frame, reason)
  if frame == nil then
    return
  end

  ChromaLink.Diagnostics.LogLayout(label, frame, reason)
end

local function FormatHeaderFlags(headerFlags)
  local labels = {}

  if headerFlags == nil then
    return "none"
  end

  if headerFlags.multiFrameRotation ~= nil and headerFlags.multiFrameRotation ~= 0 then
    table.insert(labels, "multi-frame")
  end

  if headerFlags.playerPosition ~= nil and headerFlags.playerPosition ~= 0 then
    table.insert(labels, "player-position")
  end

  if headerFlags.playerCast ~= nil and headerFlags.playerCast ~= 0 then
    table.insert(labels, "player-cast")
  end

  if headerFlags.expandedStats ~= nil and headerFlags.expandedStats ~= 0 then
    table.insert(labels, "expanded-stats")
  end

  if headerFlags.targetPosition ~= nil and headerFlags.targetPosition ~= 0 then
    table.insert(labels, "target-position")
  end

  if headerFlags.followUnitStatus ~= nil and headerFlags.followUnitStatus ~= 0 then
    table.insert(labels, "follow-unit-status")
  end

  if headerFlags.additionalTelemetry ~= nil and headerFlags.additionalTelemetry ~= 0 then
    table.insert(labels, "additional-telemetry")
  end

  if headerFlags.textAndAuras ~= nil and headerFlags.textAndAuras ~= 0 then
    table.insert(labels, "text-and-auras")
  end

  if #labels == 0 then
    return "none"
  end

  return table.concat(labels, ", ")
end

local function ResolveNextAuraPageKind(state)
  local kinds = ChromaLink.Config.auraPageKinds or {}
  local sequence = {
    kinds.playerBuffs or 1,
    kinds.playerDebuffs or 2,
    kinds.targetBuffs or 3,
    kinds.targetDebuffs or 4
  }
  local index = state.auraPageIndex or 1
  local selected = sequence[index] or sequence[1]

  state.auraPageIndex = math.fmod(index, #sequence) + 1
  return selected
end

local function ResolveNextTextKind(state)
  local kinds = ChromaLink.Config.textKindCodes or {}
  local sequence = {
    kinds.playerName or 1,
    kinds.targetName or 2,
    kinds.zoneName or 3,
    kinds.shardName or 4
  }
  local index = state.textPageIndex or 1
  local selected = sequence[index] or sequence[1]

  state.textPageIndex = math.fmod(index, #sequence) + 1
  return selected
end

local function ResolveFollowSlots()
  local followConfig = ChromaLink.Config.followUnit or {}
  local slots = {}
  local _, slot

  if type(followConfig.slots) == "table" then
    for _, slot in ipairs(followConfig.slots) do
      if tonumber(slot) ~= nil then
        table.insert(slots, math.max(1, math.min(20, math.floor(tonumber(slot)))))
      end
    end
  end

  if #slots == 0 and tonumber(followConfig.slot) ~= nil then
    table.insert(slots, math.max(1, math.min(20, math.floor(tonumber(followConfig.slot)))))
  end
  if #slots == 0 then
    table.insert(slots, 1)
  end

  return slots
end

local function ResolveNextFollowSlot(state)
  local slots = ResolveFollowSlots()
  local index = state.followSlotIndex or 1
  local selected = slots[index] or slots[1]

  state.followSlotIndex = math.fmod(index, #slots) + 1
  return selected
end

local function ResolveNextAuxCastTarget(state)
  local selectors = {}
  local selectorCodes = ChromaLink.Config.unitSelectorCodes or {}
  local slots = ResolveFollowSlots()
  local _, slot

  table.insert(selectors, {
    selectorCode = selectorCodes.target or 1,
    unitSpecifier = "player.target"
  })

  for _, slot in ipairs(slots) do
    table.insert(selectors, {
      selectorCode = (selectorCodes.groupBase or 16) + (slot - 1),
      unitSpecifier = string.format("group%02d", slot)
    })
  end

  local index = state.auxUnitCastIndex or 1
  local selected = selectors[index] or selectors[1]

  state.auxUnitCastIndex = math.fmod(index, #selectors) + 1
  return selected
end

local function ResolveNextAbilityWatchPage(state)
  local tracked = (ChromaLink.Config.abilityWatch and ChromaLink.Config.abilityWatch.trackedAbilities) or {}
  local totalPages = math.max(1, math.ceil(#tracked / 2))
  local index = state.abilityWatchPageIndex or 1
  local selected = math.max(1, math.min(totalPages, index))

  state.abilityWatchPageIndex = math.fmod(selected, totalPages) + 1
  return selected
end

local function BuildRiftMeterStatus()
  local adapter = ChromaLink.RiftMeterAdapter
  local integrationConfig = ChromaLink.Config.riftMeter or {}
  local snapshot

  if adapter == nil or adapter.BuildSnapshot == nil then
    return {
      configured = integrationConfig.enabled and true or false,
      probeStatus = integrationConfig.probeStatus and true or false,
      available = false,
      loaded = false,
      warnings = { "adapter unavailable" }
    }
  end

  if not integrationConfig.probeStatus then
    return {
      configured = integrationConfig.enabled and true or false,
      probeStatus = false,
      available = false,
      loaded = false,
      warnings = { "status probe disabled" }
    }
  end

  snapshot = adapter.BuildSnapshot() or {}
  snapshot.configured = integrationConfig.enabled and true or false
  snapshot.probeStatus = true
  return snapshot
end

local function JoinSummaryList(values)
  if type(values) ~= "table" or #values == 0 then
    return "none"
  end

  return table.concat(values, ", ")
end

local function BuildSyntheticFrame(frameKind, sequence)
  local snapshot
  local _, symbols
  local auraPageKind
  local textKind
  local auxCastTarget
  local followSlot
  local abilityWatchPage

  if frameKind == "playerVitals" then
    snapshot = ChromaLink.Gather.BuildSyntheticPlayerVitalsSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerVitalsFrame(snapshot, sequence)
  elseif frameKind == "playerPosition" then
    snapshot = ChromaLink.Gather.BuildSyntheticPlayerPositionSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerPositionFrame(snapshot, sequence)
  elseif frameKind == "playerCast" then
    snapshot = ChromaLink.Gather.BuildSyntheticPlayerCastSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerCastFrame(snapshot, sequence)
  elseif frameKind == "playerResources" then
    snapshot = ChromaLink.Gather.BuildSyntheticPlayerResourcesSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerResourcesFrame(snapshot, sequence)
  elseif frameKind == "playerCombat" then
    snapshot = ChromaLink.Gather.BuildSyntheticPlayerCombatSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerCombatFrame(snapshot, sequence)
  elseif frameKind == "riftMeterCombat" then
    snapshot = ChromaLink.Gather.BuildSyntheticRiftMeterCombatSnapshot()
    _, symbols = ChromaLink.Protocol.BuildRiftMeterCombatFrame(snapshot, sequence)
  elseif frameKind == "targetVitals" then
    snapshot = ChromaLink.Gather.BuildSyntheticTargetVitalsSnapshot()
    _, symbols = ChromaLink.Protocol.BuildTargetVitalsFrame(snapshot, sequence)
  elseif frameKind == "targetResources" then
    snapshot = ChromaLink.Gather.BuildSyntheticTargetResourcesSnapshot()
    _, symbols = ChromaLink.Protocol.BuildTargetResourcesFrame(snapshot, sequence)
  elseif frameKind == "targetPosition" then
    snapshot = ChromaLink.Gather.BuildSyntheticTargetPositionSnapshot()
    _, symbols = ChromaLink.Protocol.BuildTargetPositionFrame(snapshot, sequence)
  elseif frameKind == "auxUnitCast" then
    auxCastTarget = ResolveNextAuxCastTarget(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildSyntheticAuxUnitCastSnapshot(auxCastTarget.selectorCode)
    _, symbols = ChromaLink.Protocol.BuildAuxUnitCastFrame(snapshot, sequence)
  elseif frameKind == "followUnitStatus" then
    followSlot = ResolveNextFollowSlot(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildSyntheticFollowUnitStatusSnapshot(followSlot)
    _, symbols = ChromaLink.Protocol.BuildFollowUnitStatusFrame(snapshot, sequence)
  elseif frameKind == "auraPage" then
    auraPageKind = ResolveNextAuraPageKind(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildSyntheticAuraPageSnapshot(auraPageKind)
    _, symbols = ChromaLink.Protocol.BuildAuraPageFrame(snapshot, sequence)
  elseif frameKind == "textPage" then
    textKind = ResolveNextTextKind(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildSyntheticTextPageSnapshot(textKind)
    _, symbols = ChromaLink.Protocol.BuildTextPageFrame(snapshot, sequence)
  elseif frameKind == "abilityWatch" then
    abilityWatchPage = ResolveNextAbilityWatchPage(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildSyntheticAbilityWatchSnapshot(abilityWatchPage)
    _, symbols = ChromaLink.Protocol.BuildAbilityWatchFrame(snapshot, sequence)
  else
    snapshot = ChromaLink.Gather.BuildSyntheticCoreStatusSnapshot()
    _, symbols = ChromaLink.Protocol.BuildCoreFrame(snapshot, sequence)
    frameKind = "coreStatus"
  end

  return frameKind, snapshot, symbols
end

local function BuildLiveFrame(frameKind, sequence)
  local snapshot
  local _, symbols
  local auraPageKind
  local textKind
  local auxCastTarget
  local followSlot
  local abilityWatchPage

  if frameKind == "playerVitals" then
    snapshot = ChromaLink.Gather.BuildPlayerVitalsSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerVitalsFrame(snapshot, sequence)
  elseif frameKind == "playerPosition" then
    snapshot = ChromaLink.Gather.BuildPlayerPositionSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerPositionFrame(snapshot, sequence)
  elseif frameKind == "playerCast" then
    snapshot = ChromaLink.Gather.BuildPlayerCastSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerCastFrame(snapshot, sequence)
  elseif frameKind == "playerResources" then
    snapshot = ChromaLink.Gather.BuildPlayerResourcesSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerResourcesFrame(snapshot, sequence)
  elseif frameKind == "playerCombat" then
    snapshot = ChromaLink.Gather.BuildPlayerCombatSnapshot()
    _, symbols = ChromaLink.Protocol.BuildPlayerCombatFrame(snapshot, sequence)
  elseif frameKind == "riftMeterCombat" then
    snapshot = ChromaLink.Gather.BuildRiftMeterCombatSnapshot()
    _, symbols = ChromaLink.Protocol.BuildRiftMeterCombatFrame(snapshot, sequence)
  elseif frameKind == "targetVitals" then
    snapshot = ChromaLink.Gather.BuildTargetVitalsSnapshot()
    _, symbols = ChromaLink.Protocol.BuildTargetVitalsFrame(snapshot, sequence)
  elseif frameKind == "targetResources" then
    snapshot = ChromaLink.Gather.BuildTargetResourcesSnapshot()
    _, symbols = ChromaLink.Protocol.BuildTargetResourcesFrame(snapshot, sequence)
  elseif frameKind == "targetPosition" then
    snapshot = ChromaLink.Gather.BuildTargetPositionSnapshot()
    _, symbols = ChromaLink.Protocol.BuildTargetPositionFrame(snapshot, sequence)
  elseif frameKind == "auxUnitCast" then
    auxCastTarget = ResolveNextAuxCastTarget(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildAuxUnitCastSnapshot(auxCastTarget.selectorCode, auxCastTarget.unitSpecifier)
    _, symbols = ChromaLink.Protocol.BuildAuxUnitCastFrame(snapshot, sequence)
  elseif frameKind == "followUnitStatus" then
    followSlot = ResolveNextFollowSlot(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildFollowUnitStatusSnapshot(followSlot)
    _, symbols = ChromaLink.Protocol.BuildFollowUnitStatusFrame(snapshot, sequence)
  elseif frameKind == "auraPage" then
    auraPageKind = ResolveNextAuraPageKind(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildAuraPageSnapshot(auraPageKind)
    _, symbols = ChromaLink.Protocol.BuildAuraPageFrame(snapshot, sequence)
  elseif frameKind == "textPage" then
    textKind = ResolveNextTextKind(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildTextPageSnapshot(textKind)
    _, symbols = ChromaLink.Protocol.BuildTextPageFrame(snapshot, sequence)
  elseif frameKind == "abilityWatch" then
    abilityWatchPage = ResolveNextAbilityWatchPage(ChromaLink.Bootstrap.state or {})
    snapshot = ChromaLink.Gather.BuildAbilityWatchSnapshot(abilityWatchPage)
    _, symbols = ChromaLink.Protocol.BuildAbilityWatchFrame(snapshot, sequence)
  else
    snapshot = ChromaLink.Gather.BuildCoreStatusSnapshot()
    _, symbols = ChromaLink.Protocol.BuildCoreFrame(snapshot, sequence)
    frameKind = "coreStatus"
  end

  return frameKind, snapshot, symbols
end

local function ResolveRotationFrameKind(sequence)
  local rotation = ChromaLink.Config.frameRotation or { "coreStatus" }
  local rotationIndex = math.fmod(sequence, #rotation) + 1
  return rotation[rotationIndex] or "coreStatus"
end

function ChromaLink.Bootstrap.Refresh(forceRefresh, reason)
  local state = ChromaLink.Bootstrap.state
  local now
  local stripCount
  local stripIndex
  local symbolRows = {}
  local snapshots = {}
  local frameKinds = {}

  if state == nil then
    return
  end

  now = GetRealtimeNow()
  if not forceRefresh and now > 0 and (now - state.lastRefreshAt) < ChromaLink.Config.refreshIntervalSeconds then
    return
  end

  stripCount = ResolveStripCount()
  for stripIndex = 1, stripCount do
    local sequence = math.fmod(state.sequence + (stripIndex - 1), 256)
    local frameKind = ResolveRotationFrameKind(sequence)
    local snapshot
    local symbols

    if ChromaLink.Config.syntheticMode ~= nil and ChromaLink.Config.syntheticMode.enabled then
      frameKind, snapshot, symbols = BuildSyntheticFrame(frameKind, sequence)
    else
      frameKind, snapshot, symbols = BuildLiveFrame(frameKind, sequence)
    end

    symbolRows[stripIndex] = symbols
    snapshots[stripIndex] = snapshot
    frameKinds[stripIndex] = frameKind
  end
  ChromaLink.Render.Update(state.render, symbolRows)

  state.lastRefreshAt = now
  state.lastReason = reason
  state.lastFrameKind = frameKinds[1]
  state.lastFrameKinds = frameKinds
  state.lastSnapshot = snapshots[1]
  state.lastSnapshots = snapshots
  state.sequence = math.fmod(state.sequence + stripCount, 256)
end

function ChromaLink.Bootstrap.SafeRefresh(forceRefresh, reason)
  local ok, failure = pcall(function()
    ChromaLink.Bootstrap.Refresh(forceRefresh, reason)
  end)

  if not ok then
    ChromaLink.Diagnostics.Log("Refresh failed: " .. tostring(failure))
  end
end

function ChromaLink.Bootstrap.LogStatus(includeNativeFrames)
  local state = ChromaLink.Bootstrap.state
  local diagnosticsConfig = ChromaLink.Config.layoutDiagnostics or {}
  local observerConfig = ChromaLink.Config.observerLane or {}
  local compensationConfig = ChromaLink.Config.displayCompensation or {}
  local compensationSummary
  local rotation = ChromaLink.Config.frameRotation or {}
  local riftMeterStatus
  local warningsSummary
  local nativeFrames
  local entry

  if state == nil then
    ChromaLink.Diagnostics.Log("Status requested before initialization.")
    return
  end

  state.riftMeterStatus = BuildRiftMeterStatus()
  riftMeterStatus = state.riftMeterStatus

  ChromaLink.Diagnostics.Log(string.format(
    "Status: seq=%d frame=%s strips=%d lastReason=%s diag=%s traces=%s observer=%s.",
    tonumber(state.sequence) or 0,
    tostring(state.lastFrameKind or "unknown"),
    ResolveStripCount(),
    tostring(state.lastReason or "unknown"),
    diagnosticsConfig.enabled and "on" or "off",
    diagnosticsConfig.logEvents and "on" or "off",
    observerConfig.enabled and "on" or "off"))
  ChromaLink.Diagnostics.Log("Rotation: " .. table.concat(rotation, " -> ") .. ".")
  warningsSummary = "none"
  if type(riftMeterStatus.warnings) == "table" and #riftMeterStatus.warnings > 0 then
    warningsSummary = table.concat(riftMeterStatus.warnings, "; ")
  end
  ChromaLink.Diagnostics.Log(string.format(
    "RiftMeter: configured=%s probe=%s loaded=%s active=%s combats=%s durationMs=%s warnings=%s.",
    riftMeterStatus.configured and "on" or "off",
    riftMeterStatus.probeStatus and "on" or "off",
    riftMeterStatus.loaded and "yes" or "no",
    riftMeterStatus.inCombat and "yes" or "no",
    tostring(riftMeterStatus.combatCount or 0),
    tostring(riftMeterStatus.activeCombatDurationMs or "n/a"),
    warningsSummary))
  ChromaLink.Diagnostics.Log(string.format(
    "Anchor=%s strata=%s.",
    tostring(state.layoutAnchorReason or "context"),
    tostring(state.resolvedStrata or ChromaLink.Config.requestedStrata or "default")))

  compensationSummary = ChromaLink.Render.GetDisplayCompensationSummary(state.render)
  if compensationSummary ~= nil then
    ChromaLink.Diagnostics.Log(string.format(
      "Compensation: enabled=%s mode=%s anchorRatio=%.3fx%.3f scale=%.3fx%.3f.",
      compensationConfig.enabled and "on" or "off",
      tostring(compensationSummary.reason or compensationConfig.mode or "disabled"),
      tonumber(compensationSummary.anchorRatioX) or 1,
      tonumber(compensationSummary.anchorRatioY) or 1,
      tonumber(compensationSummary.effectiveScaleX) or 1,
      tonumber(compensationSummary.effectiveScaleY) or 1))
  end

  LogFrameStatus("layout.anchor", state.layoutAnchor, "status")
  LogFrameStatus("layout.root", state.root, "status")
  if state.render ~= nil then
    LogFrameStatus("layout.band", state.render.band, "status")
    LogFrameStatus("layout.quietZone", state.render.quietZone, "status")
    if state.render.observerLane ~= nil then
      LogFrameStatus("layout.observerLane", state.render.observerLane.bar, "status")
    end
  end

  if not includeNativeFrames then
    return
  end

  nativeFrames = {
    { label = "native.portraitPlayer", frame = UI.Native.PortraitPlayer },
    { label = "native.questStickies", frame = UI.Native.QuestStickies },
    { label = "native.mapMini", frame = UI.Native.MapMini },
    { label = "native.menu", frame = UI.Native.Menu },
    { label = "native.rift", frame = UI.Native.Rift }
  }

  for _, entry in ipairs(nativeFrames) do
    LogFrameStatus(entry.label, entry.frame, "status")
  end
end

function ChromaLink.Bootstrap.LogRiftMeterStatus(verbose)
  local state = ChromaLink.Bootstrap.state
  local status
  local warningsSummary

  if state == nil then
    ChromaLink.Diagnostics.Log("RiftMeter status requested before initialization.")
    return
  end

  state.riftMeterStatus = BuildRiftMeterStatus()
  status = state.riftMeterStatus
  warningsSummary = "none"
  if type(status.warnings) == "table" and #status.warnings > 0 then
    warningsSummary = table.concat(status.warnings, "; ")
  end

  ChromaLink.Diagnostics.Log(string.format(
    "RiftMeter status: configured=%s probe=%s loaded=%s available=%s active=%s combats=%s durationMs=%s players=%s hostiles=%s overallDurationMs=%s overallPlayers=%s overallHostiles=%s damage=%s healing=%s sampledAt=%s warnings=%s.",
    status.configured and "on" or "off",
    status.probeStatus and "on" or "off",
    status.loaded and "yes" or "no",
    status.available and "yes" or "no",
    status.inCombat and "yes" or "no",
    tostring(status.combatCount or 0),
    tostring(status.activeCombatDurationMs or "n/a"),
    tostring(status.activeCombatPlayerCount or 0),
    tostring(status.activeCombatHostileCount or 0),
    tostring(status.overallDurationMs or "n/a"),
    tostring(status.overallPlayerCount or 0),
    tostring(status.overallHostileCount or 0),
    tostring(status.overallDamage or "n/a"),
    tostring(status.overallHealing or "n/a"),
    tostring(status.sampledAt or "n/a"),
    warningsSummary))

  if not verbose then
    return
  end

  ChromaLink.Diagnostics.Log("RiftMeter top-level keys: " .. JoinSummaryList(status.topLevelKeys) .. ".")
  ChromaLink.Diagnostics.Log("RiftMeter latest combat keys: " .. JoinSummaryList(status.combatKeys) .. ".")
  ChromaLink.Diagnostics.Log("RiftMeter overall keys: " .. JoinSummaryList(status.overallKeys) .. ".")
end

function ChromaLink.Bootstrap.LogBuildStatus()
  local version = ChromaLink.Config.addonVersion or "unknown"
  local identifier = ChromaLink.Config.addonIdentifier or "ChromaLink"
  local protocolVersion = ChromaLink.Config.protocolVersion or 0
  local profile = ChromaLink.Config.profile or {}
  local frameTypes = ChromaLink.Config.frameTypes or {}
  local headerFlags = ChromaLink.Config.headerFlags or {}
  local riftMeterConfig = ChromaLink.Config.riftMeter or {}

  ChromaLink.Diagnostics.Log(string.format("%s build: version=%s protocol=%s profile=%s.", identifier, tostring(version), tostring(protocolVersion), tostring(profile.id or "unknown")))
  ChromaLink.Diagnostics.Log(string.format(
    "Frame types: coreStatus=%s playerVitals=%s playerPosition=%s playerCast=%s playerResources=%s playerCombat=%s targetPosition=%s followUnitStatus=%s targetVitals=%s targetResources=%s auxUnitCast=%s auraPage=%s textPage=%s abilityWatch=%s riftMeterCombat=%s.",
    tostring(frameTypes.coreStatus or "n/a"),
    tostring(frameTypes.playerVitals or "n/a"),
    tostring(frameTypes.playerPosition or "n/a"),
    tostring(frameTypes.playerCast or "n/a"),
    tostring(frameTypes.playerResources or "n/a"),
    tostring(frameTypes.playerCombat or "n/a"),
    tostring(frameTypes.targetPosition or "n/a"),
    tostring(frameTypes.followUnitStatus or "n/a"),
    tostring(frameTypes.targetVitals or "n/a"),
    tostring(frameTypes.targetResources or "n/a"),
    tostring(frameTypes.auxUnitCast or "n/a"),
    tostring(frameTypes.auraPage or "n/a"),
    tostring(frameTypes.textPage or "n/a"),
    tostring(frameTypes.abilityWatch or "n/a"),
    tostring(frameTypes.riftMeterCombat or "n/a")))
  ChromaLink.Diagnostics.Log(string.format(
    "Header flags: 0x%02X (%s).",
    tonumber(headerFlags.multiFrameRotation or 0)
      + tonumber(headerFlags.playerPosition or 0)
      + tonumber(headerFlags.playerCast or 0)
      + tonumber(headerFlags.expandedStats or 0)
      + tonumber(headerFlags.targetPosition or 0)
      + tonumber(headerFlags.followUnitStatus or 0)
      + tonumber(headerFlags.additionalTelemetry or 0)
      + tonumber(headerFlags.textAndAuras or 0),
    FormatHeaderFlags(headerFlags)))
  ChromaLink.Diagnostics.Log(string.format(
    "Strip profile: %s %dx%d band=%dx%d strips=%d segments=%d x %d.",
    tostring(profile.id or "unknown"),
    tonumber(profile.windowWidth or 0),
    tonumber(profile.windowHeight or 0),
    tonumber(profile.bandWidth or 0),
    tonumber(profile.bandHeight or 0),
    ResolveStripCount(),
    tonumber(profile.segmentCount or 0),
    tonumber(profile.segmentWidth or 0)))
  ChromaLink.Diagnostics.Log(string.format(
    "RiftMeter integration: enabled=%s probe=%s publish=%s.",
    riftMeterConfig.enabled and "on" or "off",
    riftMeterConfig.probeStatus and "on" or "off",
    riftMeterConfig.publishTelemetry and "on" or "off"))
end

function ChromaLink.Bootstrap.LogRotationStatus()
  local rotation = ChromaLink.Config.frameRotation or {}
  local frameTypes = ChromaLink.Config.frameTypes or {}
  local parts = {}
  local index
  local frameKind

  for index = 1, #rotation do
    frameKind = rotation[index]
    parts[index] = string.format("%02d:%s(%s)", index, tostring(frameKind), tostring(frameTypes[frameKind] or "n/a"))
  end

  ChromaLink.Diagnostics.Log("Rotation sequence: " .. (#parts > 0 and table.concat(parts, " -> ") or "none") .. ".")
  ChromaLink.Diagnostics.Log("Heartbeat priority: coreStatus is dominant; secondary telemetry slices and generic pages are rotated in for throughput without widening the strip.")
end

function ChromaLink.Bootstrap.SetObserverEnabled(enabled)
  local state = ChromaLink.Bootstrap.state
  local observerConfig = ChromaLink.Config.observerLane

  if observerConfig == nil then
    ChromaLink.Diagnostics.Log("Observer lane is unavailable in this build.")
    return
  end

  observerConfig.enabled = enabled and true or false

  if state == nil or state.render == nil then
    ChromaLink.Diagnostics.Log("Observer lane setting updated; reload the addon to apply it.")
    return
  end

  ChromaLink.Render.SetObserverEnabled(state.render, observerConfig.enabled)
  ChromaLink.Diagnostics.Log("Observer lane " .. (observerConfig.enabled and "enabled" or "disabled") .. ".")
end

function ChromaLink.Bootstrap.SetDisplayCompensationEnabled(enabled)
  local state = ChromaLink.Bootstrap.state
  local compensationConfig = ChromaLink.Config.displayCompensation

  if compensationConfig == nil then
    ChromaLink.Diagnostics.Log("Display compensation is unavailable in this build.")
    return
  end

  compensationConfig.enabled = enabled and true or false

  if state == nil or state.render == nil then
    ChromaLink.Diagnostics.Log("Display compensation updated; reload the addon to apply it.")
    return
  end

  ChromaLink.Render.SyncLayout(state.render)
  ChromaLink.Bootstrap.LogStatus(false)
end

function ChromaLink.Bootstrap.Initialize()
  if ChromaLink.Bootstrap.state ~= nil then
    return
  end

  if ChromaLink.StateCache ~= nil and ChromaLink.StateCache.Initialize ~= nil then
    ChromaLink.StateCache.Initialize()
  end

  local context = UI.CreateContext("ChromaLinkContext")
  local root = UI.CreateFrame("Frame", "ChromaLinkRoot", context)
  local renderAnchor = UIParent or context
  local renderAnchorReason = (renderAnchor == UIParent) and "UIParent" or "context"
  root:SetVisible(true)
  root:SetLayer(ChromaLink.Config.requestedLayer)

  local resolvedStrata, strataList = ResolveRequestedStrata(context)
  if resolvedStrata ~= nil then
    context:SetStrata(resolvedStrata)
  end

  ChromaLink.Bootstrap.state = {
    context = context,
    root = root,
    layoutAnchor = renderAnchor,
    layoutAnchorReason = renderAnchorReason,
    resolvedStrata = resolvedStrata,
    strataList = strataList,
    render = ChromaLink.Render.Initialize(root, renderAnchor),
    auraPageIndex = 1,
    textPageIndex = 1,
    followSlotIndex = 1,
    auxUnitCastIndex = 1,
    abilityWatchPageIndex = 1,
    riftMeterStatus = BuildRiftMeterStatus(),
    lastRefreshAt = 0,
    lastReason = "startup",
    lastFrameKind = "coreStatus",
    lastSnapshot = nil,
    sequence = 0
  }

  InstallLayoutDiagnostics(ChromaLink.Bootstrap.state)
  ChromaLink.Render.SyncLayout(ChromaLink.Bootstrap.state.render)
  ChromaLink.Diagnostics.Log("Initialized P360C segmented color strip.")
  ChromaLink.Diagnostics.Log("Layout anchor: " .. tostring(renderAnchorReason or "context") .. ".")
  if strataList ~= nil then
    ChromaLink.Diagnostics.Log("Available stratas: " .. table.concat(strataList, ", ") .. ".")
  end
  ChromaLink.Diagnostics.Log("Using strata: " .. tostring(resolvedStrata or ChromaLink.Config.requestedStrata or "default") .. ".")
  ChromaLink.Diagnostics.Log("Using stacked strips: " .. tostring(ResolveStripCount()) .. ".")
  if ChromaLink.StateCache ~= nil and ChromaLink.StateCache.LogStatus ~= nil then
    ChromaLink.StateCache.LogStatus()
  end
  if ChromaLink.Config.syntheticMode ~= nil and ChromaLink.Config.syntheticMode.enabled then
    ChromaLink.Diagnostics.Log("Synthetic strip mode is enabled.")
  end

  if ChromaLink.Bootstrap.state.render.lastRootWidth ~= nil and ChromaLink.Bootstrap.state.render.lastRootHeight ~= nil then
    ChromaLink.Diagnostics.Log(string.format(
      "Root layout %.1fx%.1f at (%.1f, %.1f) within %.1fx%.1f anchor.",
      ChromaLink.Bootstrap.state.render.lastRootWidth or 0,
      ChromaLink.Bootstrap.state.render.lastRootHeight or 0,
      ChromaLink.Bootstrap.state.render.lastRootLeft or 0,
      ChromaLink.Bootstrap.state.render.lastRootTop or 0,
      ChromaLink.Bootstrap.state.render.lastAnchorWidth or 0,
      ChromaLink.Bootstrap.state.render.lastAnchorHeight or 0))
  end

  ChromaLink.Bootstrap.SafeRefresh(true, "initialize")
end

function ChromaLink.Bootstrap.OnLoadEnd(_, loadedAddonIdentifier)
  if loadedAddonIdentifier ~= addonIdentifier then
    return
  end

  if ChromaLink.Config.showOnStartup then
    ChromaLink.Diagnostics.Log("Load event received for v" .. ChromaLink.Config.addonVersion .. ".")
    ChromaLink.Bootstrap.Initialize()
  end
end

function ChromaLink.Bootstrap.OnUpdateBegin()
  if ChromaLink.Bootstrap.state == nil then
    return
  end

  ChromaLink.Bootstrap.SafeRefresh(false, "update")
end

Command.Event.Attach(
  Event.Addon.Load.End,
  ChromaLink.Bootstrap.OnLoadEnd,
  "ChromaLink.Bootstrap.OnLoadEnd"
)

Command.Event.Attach(
  Event.System.Update.Begin,
  ChromaLink.Bootstrap.OnUpdateBegin,
  "ChromaLink.Bootstrap.OnUpdateBegin"
)
