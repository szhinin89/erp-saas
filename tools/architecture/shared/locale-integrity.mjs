/** Dictionaries are flat string maps. Read tokens before JSON.parse discards duplicates. */
export function inspectLocale(source) {
  const dictionary = JSON.parse(source);
  if (!dictionary || Array.isArray(dictionary) || typeof dictionary !== 'object' ||
      Object.values(dictionary).some(value => typeof value !== 'string')) {
    throw new Error('Expected a flat dictionary of string values');
  }
  const tokens = source.match(/"(?:\\.|[^"\\])*"|[{}\[\]:,]/g) ?? [];
  const seen = new Set();
  const duplicates = [];
  for (let i = 0; i < tokens.length - 1; i++) {
    if (tokens[i].startsWith('"') && tokens[i + 1] === ':') {
      const key = JSON.parse(tokens[i]);
      if (seen.has(key)) duplicates.push(key);
      seen.add(key);
    }
  }
  return { dictionary, duplicates };
}

export function compareLocaleKeys(reference, target) {
  return {
    missing: Object.keys(reference).filter(key => !(key in target)),
    extra: Object.keys(target).filter(key => !(key in reference)),
  };
}
