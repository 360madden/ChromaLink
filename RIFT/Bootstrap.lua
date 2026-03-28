-- script name: RIFT/Bootstrap.lua
-- version: 0.5.0
-- purpose: Initializes the ChromaLink RIFT integration, schedules live ChromaLink telemetry frames, and updates the protocol band.
-- dependencies: Core/Config.lua, Core/Gather.lua, Core/Scheduler.lua, Core/Protocol.lua, Core/Pack.lua, RIFT/Diagnostics.lua, RIFT/Render.lua
-- important assumptions: Uses Event.Addon.Load.End, Event.System.Update.Begin, and Event.Unit.Castbar. Exact highest-safe strata remains unverified.
-- protocol version: ChromaLink
-- framework module role: RIFT integration bootstrap
-- character count note: Character count not precomputed; measure with tooling if needed.

ChromaLink = ChromaLink or {}
ChromaLink.Bootstrap = {}

local addonIdentifier = ChromaLink.Config.addonIdentifier

function ChromaLink.Bootstrap.GetRealtimeNow()
  if Inspect ~= nil and Inspect.Time ~= nil and Inspect.Time.Real ~= nil then
    return Inspect.Time.Real()
  end

  return 0
end

function ChromaLink.Bootstrap.GetRefreshInterval()
  local state = ChromaLink.Bootstrap.state
  if state ~= nil and state.currentCastActive then
    return ChromaLink.Config.refreshIntervalCastingSeconds
  end

  return ChromaLink.Config.refreshIntervalSeconds
end

function ChromaLink.Bootstrap.RefreshTelemetry(forceRefresh, refreshReason)
  local state = ChromaLink.Bootstrap.state
  local now = ChromaLink.Bootstrap.GetRealtimeNow()
  local refreshInterval = ChromaLink.Bootstrap.GetRefreshInterval()

  if state == nil then
    return
  end

  if not forceRefresh and now > 0 and (now - (state.lastRefreshAt or 0)) < refreshInterval then
    return
  end

  local snapshot = ChromaLink.Gather.BuildPlayerSnapshot()
  local validation = ChromaLink.Gather.ValidateSnapshot(snapshot)
  local scheduleEntry = ChromaLink.Scheduler.NextFrame(state.scheduler)
  local frameBytes, frameMeta = ChromaLink.Protocol.BuildLiveFrameBytes(snapshot, scheduleEntry)
  local renderMetrics = ChromaLink.Render.UpdateLiveBand(state.render, snapshot, frameBytes)

  state.lastRefreshAt = now
  state.currentCastActive = snapshot.castActive and true or false
  state.lastSnapshot = snapshot
  state.lastValidation = validation
  state.lastFrameMeta = frameMeta
  state.lastRenderMetrics = renderMetrics

  if validation ~= nil then
    ChromaLink.Diagnostics.LogSnapshotValidation(validation, refreshReason)
  end

  if snapshot.debugProbe ~= nil then
    ChromaLink.Diagnostics.LogGatherProbe(snapshot.debugProbe, refreshReason)
  end
end

function ChromaLink.Bootstrap.SafeRefreshTelemetry(forceRefresh, refreshReason)
  local state = ChromaLink.Bootstrap.state
  local ok, failureMessage = pcall(function()
    ChromaLink.Bootstrap.RefreshTelemetry(forceRefresh, refreshReason)
  end)

  if ok then
    if state ~= nil then
      state.lastRefreshError = nil
    end
    return
  end

  if state ~= nil and state.lastRefreshError == failureMessage then
    return
  end

  if state ~= nil then
    state.lastRefreshError = failureMessage
  end

  ChromaLink.Diagnostics.Log("Telemetry refresh failed: " .. tostring(failureMessage))
end

function ChromaLink.Bootstrap.Initialize()
  if ChromaLink.Bootstrap.state ~= nil then
    return
  end

  local context = UI.CreateContext("ChromaLinkContext")
  local root = UI.CreateFrame("Frame", "ChromaLinkRoot", context)
  root:SetAllPoints(context)
  root:SetVisible(true)
  root:SetLayer(ChromaLink.Config.requestedLayer)

  if ChromaLink.Config.requestedStrata ~= nil then
    root:SetStrata(ChromaLink.Config.requestedStrata)
  end

  local renderState = ChromaLink.Render.InitializeLiveBand(root)
  ChromaLink.Bootstrap.state = {
    context = context,
    root = root,
    render = renderState,
    scheduler = ChromaLink.Scheduler.NewState(),
    lastRefreshAt = 0,
    currentCastActive = false,
    lastSnapshot = nil,
    lastValidation = nil,
    lastFrameMeta = nil,
    lastRenderMetrics = nil,
    lastRefreshError = nil
  }

  ChromaLink.Diagnostics.LogLoaded()
  ChromaLink.Diagnostics.Log("Initialized ChromaLink core+tactical hot-lane telemetry.")
  ChromaLink.Diagnostics.Log("Root strata options: " .. ChromaLink.Diagnostics.DescribeStrataList(root))
  ChromaLink.Diagnostics.Log("Rendered profile: " .. renderState.profile.id)
  ChromaLink.Diagnostics.Log("Refresh cadence: " .. tostring(ChromaLink.Config.refreshIntervalSeconds) .. "s base / " .. tostring(ChromaLink.Config.refreshIntervalCastingSeconds) .. "s casting.")

  ChromaLink.Bootstrap.SafeRefreshTelemetry(true, "initialize")
end

function ChromaLink.Bootstrap.OnLoadEnd(_, loadedAddonIdentifier)
  if loadedAddonIdentifier ~= addonIdentifier then
    return
  end

  if ChromaLink.Config.showOnStartup then
    ChromaLink.Diagnostics.Log("Load event received for v" .. ChromaLink.Config.addonVersion .. ".")

    local ok, failureMessage = pcall(ChromaLink.Bootstrap.Initialize)
    if not ok then
      ChromaLink.Diagnostics.Log("Initialization failed: " .. tostring(failureMessage))
    end
  end
end

function ChromaLink.Bootstrap.OnUpdateBegin()
  if ChromaLink.Bootstrap.state == nil then
    return
  end

  ChromaLink.Bootstrap.SafeRefreshTelemetry(false, "update")
end

function ChromaLink.Bootstrap.OnCastbarChanged(units)
  if ChromaLink.Bootstrap.state == nil then
    return
  end

  local playerUnitId = ChromaLink.Gather.GetPlayerUnitId()
  local playerChanged = false

  if type(units) == "table" then
    local unitId
    local visible
    for unitId, visible in pairs(units) do
      if unitId == playerUnitId or unitId == "player" then
        playerChanged = true
        if visible then
          ChromaLink.Gather.RefreshCastbarCache(unitId, "castbar-event")
        else
          ChromaLink.Gather.RefreshCastbarCache(unitId, "castbar-cleared")
        end
      end
    end
  else
    playerChanged = true
    ChromaLink.Gather.RefreshCastbarCache(playerUnitId, "castbar-event")
  end

  if not playerChanged then
    return
  end

  ChromaLink.Bootstrap.state.lastRefreshAt = 0
  ChromaLink.Bootstrap.SafeRefreshTelemetry(true, "castbar")
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

Command.Event.Attach(
  Event.Unit.Castbar,
  ChromaLink.Bootstrap.OnCastbarChanged,
  "ChromaLink.Bootstrap.OnCastbarChanged"
)

-- end-of-script marker comment
