ChromaLink = ChromaLink or {}
ChromaLink.Diagnostics = {}

local function EscapeHtml(text)
  local escaped = tostring(text)
  escaped = string.gsub(escaped, "&", "&amp;")
  escaped = string.gsub(escaped, "<", "&lt;")
  escaped = string.gsub(escaped, ">", "&gt;")
  return escaped
end

function ChromaLink.Diagnostics.Log(message)
  local formatted = "[ChromaLink] " .. tostring(message)

  if Command ~= nil and Command.Console ~= nil and Command.Console.Display ~= nil then
    local html = "<font color=\"" .. (ChromaLink.Config.chatColorHex or "#65D6FF") .. "\">" .. EscapeHtml(formatted) .. "</font>"
    Command.Console.Display("general", true, html, true)
    return
  end

  print(formatted)
end
