// Game-only public API boundary. Individual tests supply their locale/translator.
export function useLocalization(): {translate(key: string): string | null} {
 throw new Error('A test must supply the CS2 localization context.');
}
