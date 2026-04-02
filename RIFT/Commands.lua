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

local function PrintHelp()
  ChromaLink.Diagnostics.Log("Commands: /cl status | /cl diag | /cl refresh | /cl observer on|off|status | /cl traces on|off")
end

function ChromaLink.Commands.OnSlashCommand(_, commandText)
  local words = SplitWords(commandText)
  local command = string.lower(words[1] or "help")
  local option = string.lower(words[2] or "")

  if command == "status" then
    ChromaLink.Bootstrap.LogStatus(false)
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
