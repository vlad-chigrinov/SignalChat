const ANSI_COLOUR_CODES = /\u001b\[[0-9;]*m/g

/** Test runners colour their messages for a terminal; a summary page shows the codes as noise. */
export function stripAnsi(text) {
  return text.replace(ANSI_COLOUR_CODES, '')
}
