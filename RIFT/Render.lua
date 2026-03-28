-- script name: RIFT/Render.lua
-- version: 0.5.0
-- purpose: Creates and updates the live ChromaLink protocol band with a reserved top band and full interior matrix rendering.
-- dependencies: Core/Config.lua, Core/Protocol.lua, Core/Pack.lua, Core/Gather.lua, RIFT/Diagnostics.lua
-- important assumptions: Assumes Frame:SetPoint, Frame:SetBackgroundColor, and Frame:SetVisible behave per current documented RIFT UI API.
-- protocol version: ChromaLink
-- framework module role: RIFT renderer
-- character count note: Character count not precomputed; measure with tooling if needed.

ChromaLink = ChromaLink or {}
ChromaLink.Render = {}

function ChromaLink.Render.ApplyColor(frame, color)
  frame:SetBackgroundColor(color[1], color[2], color[3], color[4])
end

function ChromaLink.Render.CreateBlock(parent, name, x, y, width, height, color, layer)
  local frame = UI.CreateFrame("Frame", name, parent)
  frame:SetWidth(width)
  frame:SetHeight(height)
  frame:SetPoint("TOPLEFT", parent, "TOPLEFT", x, y)
  frame:SetLayer(layer)
  ChromaLink.Render.ApplyColor(frame, color)
  return frame
end

function ChromaLink.Render.CreateModuleFrame(parent, name, x, y, size, color, layer)
  local frame = UI.CreateFrame("Frame", name, parent)
  frame:SetWidth(size)
  frame:SetHeight(size)
  frame:SetPoint("TOPLEFT", parent, "TOPLEFT", x, y)
  frame:SetLayer(layer)
  ChromaLink.Render.ApplyColor(frame, color)
  return frame
end

function ChromaLink.Render.SetFrameVisible(frame, isVisible)
  frame:SetVisible(isVisible and true or false)
end

function ChromaLink.Render.CreatePanelBuffer(parent, namePrefix, profile, config, layer, borderPositions)
  local panel
  local borderFrames = {}
  local dataFrames = {}
  local lastBits = {}
  local dataIndex = 1
  local borderIndex = 1
  local rowIndex
  local colIndex
  local key

  panel = ChromaLink.Render.CreateBlock(
    parent,
    namePrefix .. "_SymbolPanel",
    0,
    0,
    profile.bandWidth,
    profile.bandHeight,
    config.colors.symbolPanelLight,
    layer
  )

  for key, position in pairs(borderPositions) do
    borderFrames[borderIndex] = ChromaLink.Render.CreateModuleFrame(
      panel,
      namePrefix .. "_Border_" .. tostring(borderIndex),
      profile.quietLeft + ((position.column - 1) * profile.pitch),
      profile.quietTop + ((position.row - 1) * profile.pitch),
      profile.pitch,
      config.colors.moduleDark,
      layer + 1
    )
    borderIndex = borderIndex + 1
  end

  for rowIndex = 2, profile.gridRows - 1 do
    for colIndex = 2, profile.gridColumns - 1 do
      dataFrames[dataIndex] = ChromaLink.Render.CreateModuleFrame(
        panel,
        namePrefix .. "_Data_" .. tostring(dataIndex),
        profile.quietLeft + ((colIndex - 1) * profile.pitch),
        profile.quietTop + ((rowIndex - 1) * profile.pitch),
        profile.pitch,
        config.colors.moduleDark,
        layer + 2
      )
      ChromaLink.Render.SetFrameVisible(dataFrames[dataIndex], false)
      lastBits[dataIndex] = 0
      dataIndex = dataIndex + 1
    end
  end

  return {
    panel = panel,
    borderFrames = borderFrames,
    dataFrames = dataFrames,
    lastBits = lastBits
  }
end

function ChromaLink.Render.SetBufferVisible(buffer, isVisible)
  ChromaLink.Render.SetFrameVisible(buffer.panel, isVisible)
end

function ChromaLink.Render.BuildStaticBorderPositions(profile)
  local positions = {}
  local rowIndex
  local colIndex
  local key

  for colIndex = 1, profile.gridColumns do
    key = "1:" .. tostring(colIndex)
    positions[key] = { row = 1, column = colIndex }

    if math.fmod(colIndex - 1, 2) == 0 then
      key = tostring(profile.gridRows) .. ":" .. tostring(colIndex)
      positions[key] = { row = profile.gridRows, column = colIndex }
    end
  end

  for rowIndex = 1, profile.gridRows do
    key = tostring(rowIndex) .. ":1"
    positions[key] = { row = rowIndex, column = 1 }

    if math.fmod(rowIndex - 1, 2) == 0 then
      key = tostring(rowIndex) .. ":" .. tostring(profile.gridColumns)
      positions[key] = { row = rowIndex, column = profile.gridColumns }
    end
  end

  return positions
end

function ChromaLink.Render.FrameBytesEqual(leftBytes, rightBytes)
  local index

  if leftBytes == nil or rightBytes == nil or #leftBytes ~= #rightBytes then
    return false
  end

  for index = 1, #leftBytes do
    if leftBytes[index] ~= rightBytes[index] then
      return false
    end
  end

  return true
end

function ChromaLink.Render.CopyFrameBytes(bytes)
  local copy = {}
  local index

  for index = 1, #bytes do
    copy[index] = bytes[index]
  end

  return copy
end

function ChromaLink.Render.InitializeLiveBand(rootFrame)
  local config = ChromaLink.Config
  local clientWidth, clientHeight = ChromaLink.Gather.GetClientSize()
  local profile = config.GetActiveProfile(clientWidth, clientHeight)
  local reservedBandWidth = clientWidth
  local borderPositions = ChromaLink.Render.BuildStaticBorderPositions(profile)
  local band
  local buffers = {}
  local bufferIndex

  if reservedBandWidth < profile.bandWidth then
    reservedBandWidth = profile.bandWidth
  end

  band = ChromaLink.Render.CreateBlock(
    rootFrame,
    "ChromaLink_Band",
    0,
    0,
    reservedBandWidth,
    profile.bandHeight,
    config.colors.bandReservedDark,
    config.requestedLayer
  )

  for bufferIndex = 1, 2 do
    buffers[bufferIndex] = ChromaLink.Render.CreatePanelBuffer(
      band,
      "ChromaLink_Buffer" .. tostring(bufferIndex),
      profile,
      config,
      config.requestedLayer + 1,
      borderPositions
    )
    ChromaLink.Render.SetBufferVisible(buffers[bufferIndex], bufferIndex == 1)
  end

  return {
    profile = profile,
    band = band,
    buffers = buffers,
    lastFrameBytes = nil,
    currentBandWidth = reservedBandWidth,
    activeBufferIndex = 1
  }
end

function ChromaLink.Render.ApplyReservedBandWidth(renderState, clientWidth)
  local width = clientWidth or 0

  if width < renderState.profile.bandWidth then
    width = renderState.profile.bandWidth
  end

  if width ~= renderState.currentBandWidth then
    renderState.band:SetWidth(width)
    renderState.currentBandWidth = width
  end
end

function ChromaLink.Render.UpdateLiveBand(renderState, snapshot, frameBytes)
  ChromaLink.Render.ApplyReservedBandWidth(renderState, snapshot.clientWidth)
  local activeBufferIndex = renderState.activeBufferIndex or 1
  local activeBuffer = renderState.buffers[activeBufferIndex]

  if ChromaLink.Render.FrameBytesEqual(renderState.lastFrameBytes, frameBytes) then
    return {
      changedCount = 0,
      bitCount = #activeBuffer.dataFrames,
      bandWidth = renderState.currentBandWidth,
      bytesUnchanged = true,
      swapped = false,
      activeBufferIndex = activeBufferIndex
    }
  end

  local matrix = ChromaLink.Protocol.BuildModuleMatrix(renderState.profile, frameBytes)
  local changedCount = 0
  local nextBufferIndex = activeBufferIndex == 1 and 2 or 1
  local nextBuffer = renderState.buffers[nextBufferIndex]
  local index = 1
  local rowIndex
  local colIndex

  for rowIndex = 2, renderState.profile.gridRows - 1 do
    for colIndex = 2, renderState.profile.gridColumns - 1 do
      local bit = matrix[rowIndex][colIndex] or 0
      if nextBuffer.lastBits[index] ~= bit then
        ChromaLink.Render.SetFrameVisible(nextBuffer.dataFrames[index], bit == 1)
        nextBuffer.lastBits[index] = bit
        changedCount = changedCount + 1
      end
      index = index + 1
    end
  end

  ChromaLink.Render.SetBufferVisible(nextBuffer, true)
  ChromaLink.Render.SetBufferVisible(activeBuffer, false)
  renderState.activeBufferIndex = nextBufferIndex

  renderState.lastFrameBytes = ChromaLink.Render.CopyFrameBytes(frameBytes)

  return {
    changedCount = changedCount,
    bitCount = #nextBuffer.dataFrames,
    bandWidth = renderState.currentBandWidth,
    bytesUnchanged = false,
    swapped = true,
    activeBufferIndex = nextBufferIndex
  }
end

-- end-of-script marker comment
