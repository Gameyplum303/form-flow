/**
 * Share codes, the short codes in a survey's share link that students also type on the Join screen.
 * Kept in step with FormFlow.Data/Services/ShareCodes.cs.
 */

/** The characters a code is made of. There is no 0, o, 1, l or i, so a code copied by hand comes out right. */
export const SHARE_CODE_ALPHABET = "23456789abcdefghjkmnpqrstuvwxyz";

export const SHARE_CODE_LENGTH = 8;

/**
 * The stored form of a typed or pasted code: lowercase, without spaces or dashes, and without the
 * rest of a share link around it. An empty string for nothing.
 */
export function normalizeShareCode(input: string | null | undefined): string {
    let text = (input ?? "").trim();
    const linkStart = text.toLowerCase().lastIndexOf("/s/");
    if (linkStart >= 0) {
        text = text.slice(linkStart + 3);
    }
    const end = text.search(/[/?#]/);
    if (end >= 0) {
        text = text.slice(0, end);
    }
    return text.replace(/[\s-]/g, "").toLowerCase();
}

/** Whether the text, once normalized, has the length and characters of a share code. */
export function looksLikeShareCode(input: string | null | undefined): boolean {
    const code = normalizeShareCode(input);
    return code.length === SHARE_CODE_LENGTH && code.split("").every((c) => SHARE_CODE_ALPHABET.includes(c));
}
