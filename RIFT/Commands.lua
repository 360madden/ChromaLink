ChromaLink = ChromaLink or {}
ChromaLink.Commands = {}

local function SplitWords(commandText)
  local words = {}
  local word

  for word in string.gmatch(tostring(commandText or ""), "[^%s]+") do
    table.insert(words, word)
  end

  return words
end

local function SetTraceMode(enabled)
  local diagnosticsConfig = ChromaLink.Config.layoutDiagnostics
  if diagnosticsConfig == nil then
    return
  end

  diagnosticsConfig.enabled = enabled and true or false
  diagnosticsConfig.logEvents = enabled and true or false
end

local function PrintBuildStatus()
  if ChromaLink.Bootstrap ~= nil and ChromaLink.Bootstrap.LogBuildStatus ~= nil then
    ChromaLink.Bootstrap.LogBuildStatus()
    return
  end

  ChromaLink.Diagnostics.Log("Build status is unavailable until bootstrap initialization completes.")
end

local function PrintRotationStatus()
  if ChromaLink.Bootstrap ~= nil and ChromaLink.Bootstrap.LogRotationStatus ~= nil then
    ChromaLink.Bootstrap.LogRotationStatus()
    return
  end

  ChromaLink.Diagnostics.Log("Rotation status is unavailable until bootstrap initialization completes.")
end

local function PrintHelp()
  ChromaLink.Diagnostics.Log("Commands: /cl status | /cl build | /cl rotation | /cl diag | /cl refresh | /cl observer on|off|status | /cl compensate on|off|status | /cl traces on|off")
end

function ChromaLink.Commands.OnSlashCommand(_, commandText)
  local words = SplitWords(commandText)
  local command = string.lower(words[1] or "help")
  local option = string.lower(words[2] or "")

  if command == "status" then
    ChromaLink.Bootstrap.LogStatus(false)
    return
  end

  if command == "build" or command == "version" or command == "caps" then
    PrintBuildStatus()
    return
  end

  if command == "rotation" or command == "rotate" then
    PrintRotationStatus()
    return
  end

  if command == "diag" then
    ChromaLink.Bootstrap.LogStatus(true)
    return
  end

  if command == "refresh" then
    ChromaLink.Bootstrap.SafeRefresh(true, "slash-refresh")
    ChromaLink.Diagnostics.Log("Forced strip refresh requested.")
    return
  end

  if command == "observer" then
    if option == "on" then
      ChromaLink.Bootstrap.SetObserverEnabled(true)
      return
    end

    if option == "off" then
      ChromaLink.Bootstrap.SetObserverEnabled(false)
      return
    end

    if option == "status" or option == "" then
      ChromaLink.Diagnostics.Log("Observer lane is " .. ((ChromaLink.Config.observerLane or {}).enabled and "on" or "off") .. ".")
      return
    end
  end

  if command == "traces" then
    if option == "on" then
      SetTraceMode(true)
      ChromaLink.Diagnostics.Log("Layout traces armed for this session. Run /reloadui to recreate the probe frames and attach trace hooks.")
      return
    end

    if option == "off" then
      SetTraceMode(false)
      ChromaLink.Diagnostics.Log("Layout traces disabled for this session. Run /reloadui to remove existing probe frames and trace hooks.")
      return
    end
  end

  if command == "compensate" or command == "compensation" then
    if option == "on" then
      ChromaLink.Bootstrap.SetDisplayCompensationEnabled(true)
      return
    end

    if option == "off" then
      ChromaLink.Bootstrap.SetDisplayCompensationEnabled(false)
      return
    end

    if option == "status" or option == "" then
      ChromaLink.Bootstrap.LogStatus(false)
      return
    end
  end

  PrintHelp()
end

Command.Event.Attach(
  Command.Slash.Register("cl"),
  ChromaLink.Commands.OnSlashCommand,
  "ChromaLink.Commands.OnSlashCommand.cl")

Command.Event.Attach(
  Command.Slash.Register("chromalink"),
  ChromaLink.Commands.OnSlashCommand,
  "ChromaLink.Commands.OnSlashCommand.chromalink")
