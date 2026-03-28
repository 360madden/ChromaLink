ChromaLink = ChromaLink or {}
ChromaLink.Render = {}

local function ApplyColor(frame, color)
  frame:SetBackgroundColor(color[1], color[2], color[3], color[4])
end

local function CreateBlock(parent, name, x, y, width, height, color, layer)
  local frame = UI.CreateFrame("Frame", name, parent)
  frame:SetPoint("TOPLEFT", parent, "TOPLEFT", x, y)
  frame:SetWidth(width)
  frame:SetHeight(height)
  frame:SetLayer(layer)
  frame:SetVisible(true)
  ApplyColor(frame, color)
  return frame
end

function ChromaLink.Render.Initialize(rootFrame, clientWidth)
  local config = ChromaLink.Config
  local bandWidth = config.GetBandWidth(clientWidth)
  local band = CreateBlock(
    rootFrame,
    "ChromaLinkBand",
    config.band.left,
    config.band.top,
    bandWidth,
    config.band.height,
    config.colors.bandBackground,
    config.requestedLayer
  )
  local modules = {}
  local x = config.band.padding
  local index

  for index = 1, config.band.moduleCount do
    modules[index] = CreateBlock(
      band,
      "ChromaLinkModule" .. tostring(index),
      x,
      config.band.padding,
      config.band.moduleSize,
      config.band.moduleSize,
      config.colors.moduleOff,
      config.requestedLayer + 1
    )
    x = x + config.band.moduleSize + config.band.moduleGap
  end

  return {
    band = band,
    modules = modules,
    bandWidth = bandWidth
  }
end

function ChromaLink.Render.SetBandWidth(renderState, clientWidth)
  local width = ChromaLink.Config.GetBandWidth(clientWidth)

  if width ~= renderState.bandWidth then
    renderState.band:SetWidth(width)
    renderState.bandWidth = width
  end
end

function ChromaLink.Render.Update(renderState, clientWidth, pulseIndex)
  local config = ChromaLink.Config
  local tail = config.band.pulseTail or 1
  local index

  ChromaLink.Render.SetBandWidth(renderState, clientWidth)

  for index = 1, #renderState.modules do
    local distance = pulseIndex - index
    local color = config.colors.moduleOff

    if distance == 0 then
      color = config.colors.moduleAccent
    elseif distance > 0 and distance <= tail then
      color = config.colors.moduleOn
    end

    ApplyColor(renderState.modules[index], color)
  end
end
