// The installer copies source code and approved assets, never development/test artwork.
export function includeAddonPath(relative) {
  const p=relative.replaceAll('\\','/');
  return !/(?:^|\/)generated\/(?:dev|test)(?:\/|$)/.test(p) && !/(?:^|\/)(?:legacy_art|\.art_quarantine)(?:\/|$)/.test(p);
}
