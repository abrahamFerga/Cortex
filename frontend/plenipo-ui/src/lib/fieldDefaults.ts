import type { TabEditorField } from "./api";

/**
 * What a server-declared form starts as, shared by the generic tab editor and the setup wizard.
 *
 * The shell still never pre-picks an option on its own — that guessing is exactly what the blank
 * "Choose…" entry exists to prevent. This is the other thing: a field SAYING what it should start
 * as. `default` is a constant the manifest knows; `defaultFrom` is for what only the viewer's
 * browser knows, because a manifest declared at startup cannot know where the viewer lives.
 *
 * A default is a starting point, never a value posted behind the user's back — they can clear or
 * change it, and an untouched empty field still posts nothing.
 */

/** Values only the browser can answer. Keep in sync with FieldDefaultSources (C#). */
const SOURCES: Record<string, () => string | undefined> = {
  "browser-timezone": browserTimeZone,
  "browser-currency": browserCurrency,
};

function browserTimeZone(): string | undefined {
  try {
    // Absent in exotic/locked-down runtimes; a missing time zone is not worth throwing over.
    return Intl.DateTimeFormat().resolvedOptions().timeZone || undefined;
  } catch {
    return undefined;
  }
}

// ISO-3166 region → ISO-4217 currency, for the currencies people commonly hold. This is a GUESS
// map, not a source of truth: a region with no entry simply yields no guess (the field stays on
// "Choose…", exactly as before), and any guess is still validated against the field's options and
// remains the user's to change. Eurozone members all map to EUR.
const REGION_CURRENCY: Record<string, string> = {
  US: "USD", CA: "CAD", MX: "MXN", BR: "BRL", AR: "ARS", CL: "CLP", CO: "COP", PE: "PEN",
  GB: "GBP", IE: "EUR", FR: "EUR", DE: "EUR", ES: "EUR", PT: "EUR", IT: "EUR", NL: "EUR",
  BE: "EUR", AT: "EUR", FI: "EUR", GR: "EUR", LU: "EUR", SK: "EUR", SI: "EUR", EE: "EUR",
  LV: "EUR", LT: "EUR", CY: "EUR", MT: "EUR", HR: "EUR",
  CH: "CHF", SE: "SEK", NO: "NOK", DK: "DKK", PL: "PLN", CZ: "CZK", HU: "HUF", RO: "RON",
  BG: "BGN", IS: "ISK", TR: "TRY", RU: "RUB", UA: "UAH",
  CN: "CNY", JP: "JPY", KR: "KRW", IN: "INR", ID: "IDR", TH: "THB", VN: "VND", PH: "PHP",
  MY: "MYR", SG: "SGD", HK: "HKD", TW: "TWD", PK: "PKR", BD: "BDT", LK: "LKR",
  AU: "AUD", NZ: "NZD",
  ZA: "ZAR", NG: "NGN", EG: "EGP", KE: "KES", GH: "GHS", MA: "MAD",
  AE: "AED", SA: "SAR", IL: "ILS", QA: "QAR", KW: "KWD", BH: "BHD", OM: "OMR", JO: "JOD",
};

function browserCurrency(): string | undefined {
  try {
    const lang = typeof navigator !== "undefined" ? navigator.language : undefined;
    if (!lang) return undefined;
    // maximize() fills in the LIKELY region when the tag omits it: "es" → "es-ES", "en" → "en-US".
    const region = new Intl.Locale(lang).maximize().region;
    return region ? REGION_CURRENCY[region] : undefined;
  } catch {
    return undefined;
  }
}

/** The starting value for one field: "" unless it declares a default the field can actually take. */
export function resolveFieldDefault(field: TabEditorField): string {
  const dynamic = field.defaultFrom ? SOURCES[field.defaultFrom]?.() : undefined;
  const candidate = dynamic ?? field.default ?? "";
  if (!candidate) return "";

  // A default the field would reject is worse than none: offering a value whose own endpoint
  // 400s it just moves the confusion later. Options loaded from an endpoint aren't known here,
  // so only a declared vocabulary is checked.
  if (field.options && !field.options.some((o) => o.value === candidate)) return "";
  return candidate;
}

/** Starting values for a whole form, keyed by field name. */
export function resolveFieldDefaults(fields: TabEditorField[]): Record<string, string> {
  return Object.fromEntries(fields.map((f) => [f.field, resolveFieldDefault(f)]));
}
