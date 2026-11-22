/** Keep documentation excerpts anchored to actual, build-checked source files. */
export function excerpt(source: string, from: string, until?: string): string {
  const start = source.indexOf(from);
  if (start < 0) throw new Error(`Documentation source marker missing: ${from}`);
  const end = until === undefined ? source.length : source.indexOf(until, start + from.length);
  if (end < 0) throw new Error(`Documentation source marker missing: ${until}`);
  return source.slice(start, end).trim();
}
