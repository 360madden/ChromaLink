-- script name: Core/Scheduler.lua
-- version: 0.5.0
-- purpose: Provides deterministic frame-type and lane scheduling for ChromaLink live telemetry frames.
-- dependencies: Core/Config.lua
-- important assumptions: Phase 1 alternates a core status frame with a tactical frame on the hot lane to keep the baseline simple and debuggable.
-- protocol version: ChromaLink
-- framework module role: Core frame scheduling
-- character count note: Character count not precomputed; measure with tooling if needed.

ChromaLink = ChromaLink or {}
ChromaLink.Scheduler = {}

function ChromaLink.Scheduler.NewState()
  return {
    sequence = 0,
    frameIndex = 0
  }
end

function ChromaLink.Scheduler.NextFrame(state)
  local schedulerState = state or ChromaLink.Scheduler.NewState()
  local sequence = schedulerState.sequence or 0
  local framePattern = ChromaLink.Config.frameSchedulePattern or {
    {
      frameTypeId = ChromaLink.Config.frameTypeIds.coreStatus,
      laneId = ChromaLink.Config.laneIds.hot
    }
  }
  local patternIndex = math.fmod(schedulerState.frameIndex or 0, #framePattern) + 1
  local patternEntry = framePattern[patternIndex] or {}
  local entry = {
    frameTypeId = patternEntry.frameTypeId or ChromaLink.Config.frameTypeIds.coreStatus,
    laneId = patternEntry.laneId or ChromaLink.Config.laneIds.hot,
    name = patternEntry.name or "core-status",
    sequence = sequence
  }

  schedulerState.sequence = math.fmod(sequence + 1, 0x100)
  schedulerState.frameIndex = (schedulerState.frameIndex or 0) + 1

  return entry
end

-- end-of-script marker comment
