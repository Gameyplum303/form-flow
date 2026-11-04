import { looksLikeShareCode, normalizeShareCode, SHARE_CODE_ALPHABET } from "../../FormFlow.React/src/logic/shareCodes";

describe("share codes", () => {
    test.each([
        ["k7m2p9qa", "k7m2p9qa"],
        ["K7M2P9QA", "k7m2p9qa"],
        ["  k7m2 p9qa ", "k7m2p9qa"],
        ["K7M2-P9QA", "k7m2p9qa"],
        ["https://formflow.example/s/K7M2P9QA", "k7m2p9qa"],
        ["formflow.example/s/k7m2p9qa?utm=qr", "k7m2p9qa"],
        ["http://localhost:3000/#/s/k7m2p9qa", "k7m2p9qa"],
        ["", ""],
        ["   ", ""],
    ])("normalizes %p to %p", (typed, expected) => {
        expect(normalizeShareCode(typed)).toBe(expected);
    });

    test("treats nothing as an empty code", () => {
        expect(normalizeShareCode(undefined)).toBe("");
        expect(normalizeShareCode(null)).toBe("");
    });

    test.each([
        ["K7M2 P9QA", true],
        ["k7m2p9q", false],
        ["k7m2p9qaa", false],
        ["k7m2p9q0", false],
        ["k7m2p9ql", false],
    ])("%p looks like a code: %p", (typed, valid) => {
        expect(looksLikeShareCode(typed)).toBe(valid);
    });

    test("uses the same alphabet as the API, without look-alike characters", () => {
        expect(SHARE_CODE_ALPHABET).toBe("23456789abcdefghjkmnpqrstuvwxyz");
    });
});
