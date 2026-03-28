ChromaLink = ChromaLink or {}
ChromaLink.Bootstrap = {}

local addonIdentifier = ChromaLink.Config.addonIdentifier

function ChromaLink.Bootstrap.GetRealtimeNow()
  if Inspect ~= nil and Inspect.Time ~= nil and Inspect.Time.Real ~= nil then
    return Inspect.Time.Real()
  end

  return 0
end

function ChromaLink.Bootstrap.GetClientSize()
  if UIParent == nil or UIParent.GetWidth == nil or UIParent.GetHeight == nil then
    return 0, 0
  end

  local ok, width, height = pcall(function()
    return UIParent:GetWidth(), UIParent:GetHeight()
  end)

  if not ok then
    return 0, 0
  end

  return math.floor((tonumber(width) or 0) + 0.5), math.floor((tonumber(height) or 0) + 0.5)
end

function ChromaLink.Bootstrap.Initialize()
  if ChromaLink.Bootstrap.state ~= nil then
    return
  end

  local clientWidth = ChromaLink.Bootstrap.GetClientSize()
  local context = UI.CreateContext("ChromaLinkContext")
  local root = UI.CreateFrame("Frame", "ChromaLinkRoot", context)
  root:SetAllPoints(context)
  root:SetVisible(true)
  root:SetLayer(ChromaLink.Config.requestedLayer)

  if ChromaLink.Config.requestedStrata ~= nil then
    root:SetStrata(ChromaLink.Config.requestedStrata)
  end

  local renderState = ChromaLink.Render.Initialize(root, clientWidth)
  ChromaLink.Bootstrap.state = {
    context = context,
    root = root,
    render = renderState,
    pulseIndex = 1,
    lastTickAt = 0
  }

  ChromaLink.Render.Update(renderState, clientWidth, 1)
  ChromaLink.Diagnostics.LogLoaded()
  ChromaLink.Diagnostics.Log("Fresh baseline initialized. The moving strip is only a proof of life.")
end

function ChromaLink.Bootstrap.OnLoadEnd(_, loadedAddonIdentifier)
  if loadedAddonIdentifier ~= addonIdentifier or not ChromaLink.Config.showOnStartup then
    return
  end

  local ok, failureMessage = pcall(ChromaLink.Bootstrap.Initialize)
  if not ok then
    ChromaLink.Diagnostics.Log("Initialization failed: " .. tostring(failureMessage))
  end
end

function ChromaLink.Bootstrap.OnUpdateBegin()
  local state = ChromaLink.Bootstrap.state

  if state == nil then
    return
  end

  local now = ChromaLink.Bootstrap.GetRealtimeNow()
  if now > 0 and (now - (state.lastTickAt or 0)) < ChromaLink.Config.tickIntervalSeconds then
    return
  end

  local clientWidth = ChromaLink.Bootstrap.GetClientSize()
  state.lastTickAt = now
  state.pulseIndex = state.pulseIndex + 1

  if state.pulseIndex > ChromaLink.Config.band.moduleCount then
    state.pulseIndex = 1
  end

  ChromaLink.Render.Update(state.render, clientWidth, state.pulseIndex)
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
