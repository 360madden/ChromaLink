ChromaLink = ChromaLink or {}
ChromaLink.Bootstrap = {}

local addonIdentifier = ChromaLink.Config.addonIdentifier

local function GetRealtimeNow()
  if Inspect ~= nil and Inspect.Time ~= nil and Inspect.Time.Real ~= nil then
    return Inspect.Time.Real()
  end
  return 0
end

function ChromaLink.Bootstrap.Refresh(forceRefresh, reason)
  local state = ChromaLink.Bootstrap.state
  local now
  local snapshot
  local _, symbols

  if state == nil then
    return
  end

  now = GetRealtimeNow()
  if not forceRefresh and now > 0 and (now - state.lastRefreshAt) < ChromaLink.Config.refreshIntervalSeconds then
    return
  end

  snapshot = ChromaLink.Gather.BuildCoreStatusSnapshot()
  _, symbols = ChromaLink.Protocol.BuildCoreFrame(snapshot, state.sequence)
  ChromaLink.Render.Update(state.render, symbols)

  state.lastRefreshAt = now
  state.lastReason = reason
  state.lastSnapshot = snapshot
  state.sequence = math.fmod(state.sequence + 1, 256)
end

function ChromaLink.Bootstrap.SafeRefresh(forceRefresh, reason)
  local ok, failure = pcall(function()
    ChromaLink.Bootstrap.Refresh(forceRefresh, reason)
  end)

  if not ok then
    ChromaLink.Diagnostics.Log("Refresh failed: " .. tostring(failure))
  end
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
    context:SetStrata(ChromaLink.Config.requestedStrata)
  end

  ChromaLink.Bootstrap.state = {
    context = context,
    root = root,
    render = ChromaLink.Render.Initialize(root),
    lastRefreshAt = 0,
    lastReason = "startup",
    lastSnapshot = nil,
    sequence = 0
  }

  ChromaLink.Diagnostics.Log("Initialized P360C segmented color strip.")
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
