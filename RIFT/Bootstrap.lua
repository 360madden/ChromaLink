ChromaLink = ChromaLink or {}
ChromaLink.Bootstrap = {}

local addonIdentifier = ChromaLink.Config.addonIdentifier

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
  ChromaLink.Diagnostics.AttachLayoutTrace(state.render.band, "layout.band")

  if state.render.probeBar ~= nil then
    ChromaLink.Diagnostics.AttachLayoutTrace(state.render.probeBar.bar, "layout.probeBar")
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

  if ChromaLink.Config.syntheticMode ~= nil and ChromaLink.Config.syntheticMode.enabled then
    snapshot = ChromaLink.Gather.BuildSyntheticCoreStatusSnapshot()
  else
    snapshot = ChromaLink.Gather.BuildCoreStatusSnapshot()
  end
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
    lastRefreshAt = 0,
    lastReason = "startup",
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
