ChromaLink = ChromaLink or {}
ChromaLink.Render = {}

local config = ChromaLink.Config
local function ApplyColor(frame, color)
  frame:SetBackgroundColor(color[1], color[2], color[3], color[4])
end

local function ReadDimension(frame, methodName, fallback)
  if frame == nil or frame[methodName] == nil then
    return fallback
  end

  local ok, value = pcall(function()
    return frame[methodName](frame)
  end)

  value = tonumber(value) or fallback
  if not ok or value == nil or value <= 0 then
    return fallback
  end

  return value
end

local function CreateObserverLane(rootFrame, profile)
  local observerConfig = config.observerLane
  if observerConfig == nil then
    return nil
  end

  local probeBar = UI.CreateFrame("Frame", "ChromaLinkObserverLane", rootFrame)
  local markers = {}
  local symbols = observerConfig.markerSymbols or {}
  local backgroundColor = observerConfig.backgroundColor or { 0.45, 0.45, 0.45, 0.95 }

  probeBar:SetPoint(
    "TOPLEFT",
    rootFrame,
    "TOPLEFT",
    0,
    observerConfig.offsetY or 32)
  probeBar:SetWidth(profile.bandWidth)
  probeBar:SetHeight(observerConfig.height or 8)
  probeBar:SetLayer(config.requestedLayer + 2)
  ApplyColor(probeBar, backgroundColor)
  probeBar:SetVisible(observerConfig.enabled and true or false)

  for index, symbol in ipairs(symbols) do
    local marker = UI.CreateFrame("Frame", "ChromaLinkObserverMarker" .. tostring(index), probeBar)
    marker:SetLayer(config.requestedLayer + 3)
    ApplyColor(marker, config.GetPaletteColor(symbol))
    marker:SetVisible(observerConfig.enabled and true or false)
    markers[index] = {
      frame = marker,
      fraction = (#symbols > 1) and ((index - 1) / (#symbols - 1)) or 0
    }
  end

  return {
    bar = probeBar,
    markers = markers,
    enabled = observerConfig.enabled and true or false
  }
end

local function CreateQuietZone(rootFrame)
  local quietZoneConfig = config.quietZone
  if quietZoneConfig == nil or not quietZoneConfig.enabled then
    return nil
  end

  local quietZone = UI.CreateFrame("Frame", "ChromaLinkQuietZone", rootFrame)
  quietZone:SetPoint("TOPLEFT", rootFrame, "TOPLEFT", 0, 0)
  quietZone:SetLayer(config.requestedLayer - 1)
  ApplyColor(quietZone, config.GetPaletteColor(quietZoneConfig.backgroundSymbol or 0))
  return quietZone
end

local function ComputeLayout(renderState)
  local profile = renderState.profile
  local quietZoneConfig = config.quietZone or {}
  local observerConfig = config.observerLane or {}
  local anchorFrame = renderState.anchorFrame or UIParent or renderState.rootFrame
  local anchorWidth = ReadDimension(anchorFrame, "GetWidth", profile.windowWidth)
  local anchorHeight = ReadDimension(anchorFrame, "GetHeight", profile.windowHeight)
  local rootWidth = profile.bandWidth
  local rootHeight = profile.bandHeight
  local requestedLeft = tonumber(config.stripOffsetX) or 0
  local requestedTop = tonumber(config.stripOffsetY) or 0

  if quietZoneConfig.enabled and quietZoneConfig.fullAnchorWidth then
    rootWidth = anchorWidth
    requestedLeft = 0
  end

  if quietZoneConfig.enabled then
    rootHeight = math.max(rootHeight, tonumber(quietZoneConfig.height) or profile.bandHeight)
  end

  if renderState.observerLane ~= nil and renderState.observerLane.enabled then
    local probeBarHeight = tonumber(observerConfig.height) or 12
    local probeBarBottom = (tonumber(observerConfig.offsetY) or 32) + probeBarHeight
    if probeBarBottom > rootHeight then
      rootHeight = probeBarBottom
    end
  end

  local maxLeft = math.max(0, anchorWidth - rootWidth)
  local maxTop = math.max(0, anchorHeight - rootHeight)
  local rootLeft = math.max(0, math.min(requestedLeft, maxLeft))
  local rootTop = math.max(0, math.min(requestedTop, maxTop))

  return {
    anchorFrame = anchorFrame,
    anchorWidth = anchorWidth,
    anchorHeight = anchorHeight,
    rootLeft = rootLeft,
    rootTop = rootTop,
    rootWidth = rootWidth,
    rootHeight = rootHeight
  }
end

local function ApplyLayout(renderState)
  local profile = renderState.profile
  local observerConfig = config.observerLane or {}
  local layout = ComputeLayout(renderState)
  local displayScaleX = tonumber(profile.displayScaleX) or tonumber(profile.displayScale) or 1
  local displayScaleY = tonumber(profile.displayScaleY) or tonumber(profile.displayScale) or 1
  local index

  if displayScaleX <= 0 then
    displayScaleX = 1
  end
  if displayScaleY <= 0 then
    displayScaleY = 1
  end

  local bandWidth = profile.bandWidth * displayScaleX
  local bandHeight = profile.bandHeight * displayScaleY

  if renderState.lastRootLeft == layout.rootLeft
      and renderState.lastRootTop == layout.rootTop
      and renderState.lastRootWidth == layout.rootWidth
      and renderState.lastRootHeight == layout.rootHeight
      and renderState.lastAnchorWidth == layout.anchorWidth
      and renderState.lastAnchorHeight == layout.anchorHeight then
    return false
  end

  if renderState.rootFrame.ClearAllPoints ~= nil then
    renderState.rootFrame:ClearAllPoints()
  end
  renderState.rootFrame:SetPoint("TOPLEFT", layout.anchorFrame, "TOPLEFT", layout.rootLeft, layout.rootTop)
  renderState.rootFrame:SetWidth(layout.rootWidth)
  renderState.rootFrame:SetHeight(layout.rootHeight)

  if renderState.band.ClearAllPoints ~= nil then
    renderState.band:ClearAllPoints()
  end
  renderState.band:SetPoint("TOPLEFT", renderState.rootFrame, "TOPLEFT", 0, 0)
  renderState.band:SetWidth(bandWidth)
  renderState.band:SetHeight(bandHeight)

  if renderState.quietZone ~= nil then
    if renderState.quietZone.ClearAllPoints ~= nil then
      renderState.quietZone:ClearAllPoints()
    end
    renderState.quietZone:SetPoint("TOPLEFT", renderState.rootFrame, "TOPLEFT", 0, 0)
    renderState.quietZone:SetWidth(layout.rootWidth)
    renderState.quietZone:SetHeight(layout.rootHeight)
  end

  for index = 1, profile.segmentCount do
    local segment = renderState.segments[index]
    if segment.ClearAllPoints ~= nil then
      segment:ClearAllPoints()
    end
    segment:SetPoint("TOPLEFT", renderState.band, "TOPLEFT", (index - 1) * profile.segmentWidth * displayScaleX, 0)
    segment:SetWidth(profile.segmentWidth * displayScaleX)
    segment:SetHeight(profile.segmentHeight * displayScaleY)
  end

  if renderState.observerLane ~= nil then
    local probeBarLeft = 0
    local probeBarTop = observerConfig.offsetY or 32
    local probeBarWidth = bandWidth
    local probeBarHeight = observerConfig.height or 12
    local markerWidth = observerConfig.markerWidth or 20
    local markerHeight = probeBarHeight

    if renderState.observerLane.bar.ClearAllPoints ~= nil then
      renderState.observerLane.bar:ClearAllPoints()
    end
    renderState.observerLane.bar:SetPoint("TOPLEFT", renderState.rootFrame, "TOPLEFT", probeBarLeft, probeBarTop)
    renderState.observerLane.bar:SetWidth(probeBarWidth)
    renderState.observerLane.bar:SetHeight(probeBarHeight)
    renderState.observerLane.bar:SetVisible(renderState.observerLane.enabled)

    for index = 1, #renderState.observerLane.markers do
      local markerState = renderState.observerLane.markers[index]
      local marker = markerState.frame
      local x = markerState.fraction * math.max(0, probeBarWidth - markerWidth)

      if marker.ClearAllPoints ~= nil then
        marker:ClearAllPoints()
      end
      marker:SetPoint("TOPLEFT", renderState.observerLane.bar, "TOPLEFT", x, 0)
      marker:SetWidth(markerWidth)
      marker:SetHeight(markerHeight)
      marker:SetVisible(renderState.observerLane.enabled)
    end
  end

  renderState.lastRootLeft = layout.rootLeft
  renderState.lastRootTop = layout.rootTop
  renderState.lastRootWidth = layout.rootWidth
  renderState.lastRootHeight = layout.rootHeight
  renderState.lastAnchorWidth = layout.anchorWidth
  renderState.lastAnchorHeight = layout.anchorHeight
  return true
end

function ChromaLink.Render.Initialize(rootFrame, anchorFrame)
  local profile = config.profile
  local defaultScaleX = tonumber(profile.displayScaleX) or tonumber(profile.displayScale) or 1
  local defaultScaleY = tonumber(profile.displayScaleY) or tonumber(profile.displayScale) or 1
  local quietZone = CreateQuietZone(rootFrame)
  local band = UI.CreateFrame("Frame", "ChromaLinkBand", rootFrame)
  local observerLane = CreateObserverLane(rootFrame, profile)
  local segments = {}
  local lastSymbols = {}
  local index

  band:SetPoint("TOPLEFT", rootFrame, "TOPLEFT", 0, 0)
  band:SetWidth(profile.bandWidth * defaultScaleX)
  band:SetHeight(profile.bandHeight * defaultScaleY)
  band:SetLayer(config.requestedLayer)
  ApplyColor(band, config.GetPaletteColor(0))

  for index = 1, profile.segmentCount do
    local segment = UI.CreateFrame("Frame", "ChromaLinkSegment" .. tostring(index), band)
    segment:SetPoint(
      "TOPLEFT",
      band,
      "TOPLEFT",
      (index - 1) * profile.segmentWidth * defaultScaleX,
      0)
    segment:SetWidth(profile.segmentWidth * defaultScaleX)
    segment:SetHeight(profile.segmentHeight * defaultScaleY)
    segment:SetLayer(config.requestedLayer + 1)
    ApplyColor(segment, config.GetPaletteColor(0))
    segments[index] = segment
    lastSymbols[index] = -1
  end

  return {
    profile = profile,
    rootFrame = rootFrame,
    anchorFrame = anchorFrame,
    quietZone = quietZone,
    band = band,
    observerLane = observerLane,
    segments = segments,
    lastSymbols = lastSymbols,
    lastRootLeft = nil,
    lastRootTop = nil,
    lastRootWidth = nil,
    lastRootHeight = nil,
    lastAnchorWidth = nil,
    lastAnchorHeight = nil
  }
end

function ChromaLink.Render.SyncLayout(renderState)
  return ApplyLayout(renderState)
end

function ChromaLink.Render.SetObserverEnabled(renderState, enabled)
  if renderState == nil or renderState.observerLane == nil then
    return false
  end

  renderState.observerLane.enabled = enabled and true or false
  return ApplyLayout(renderState)
end

function ChromaLink.Render.Update(renderState, symbols)
  local changed = false
  local index

  if ApplyLayout(renderState) then
    changed = true
  end

  for index = 1, renderState.profile.segmentCount do
    local symbol = symbols[index] or 0
    if renderState.lastSymbols[index] ~= symbol then
      ApplyColor(renderState.segments[index], config.GetPaletteColor(symbol))
      renderState.lastSymbols[index] = symbol
      changed = true
    end
  end

  return changed
end
