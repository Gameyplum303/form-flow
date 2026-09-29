import { expect, test } from "@playwright/test";
import { admin, adminHeaders, demoSurveyTitle, headersFor, newSignUp, professor, publish, runId, surveyByTitle, urls } from "./helpers";

test.describe("API", () => {
    test("serves Swagger UI and the OpenAPI document", async ({ request }) => {
        expect((await request.get(`${urls.api}/swagger/index.html`)).status()).toBe(200);
        const doc = await (await request.get(`${urls.api}/openapi/v1.json`)).json();
        expect(Object.keys(doc.paths)).toEqual(expect.arrayContaining([
            "/api/questions", "/api/surveys/{surveyId}/responses", "/api/surveys/{surveyId}/results",
        ]));
    });

    test("requires an admin sign-in to change surveys or read responses", async ({ request }) => {
        const survey = await surveyByTitle(request, demoSurveyTitle);
        expect((await request.post(`${urls.api}/api/questions`, { data: { key: `anon_${runId}`, label: "Anon", type: "text" } })).status()).toBe(401);
        expect((await request.delete(`${urls.api}/api/surveys/${survey.id}`)).status()).toBe(401);
        expect((await request.get(`${urls.api}/api/surveys/${survey.id}/responses`)).status()).toBe(401);
        expect((await request.get(`${urls.api}/api/surveys/${survey.id}/responses/export`)).status()).toBe(401);
        expect((await request.get(`${urls.api}/api/surveys/${survey.id}/responses`, {
            headers: { Authorization: "Bearer not-a-real-token" },
        })).status()).toBe(401);

        const wrong = await request.post(`${urls.api}/api/auth/login`, { data: { username: admin.username, password: "wrong" } });
        expect(wrong.status()).toBe(401);

        const me = await request.get(`${urls.api}/api/auth/me`, { headers: await adminHeaders(request) });
        expect(me.status()).toBe(200);
        expect(await me.json()).toEqual({ id: expect.any(String), username: admin.username, role: "admin" });
    });

    test("lets professors sign up, then sign in once an administrator approves them", async ({ request }) => {
        const signUp = newSignUp("Api Ada");
        const created = await request.post(`${urls.api}/api/auth/signup`, { data: signUp });
        expect(created.status()).toBe(201);
        expect(await created.json()).toEqual({ status: "pending" });
        expect((await request.post(`${urls.api}/api/auth/signup`, { data: signUp })).status()).toBe(409);

        const credentials = { username: signUp.email, password: signUp.password };
        const waiting = await request.post(`${urls.api}/api/auth/login`, { data: credentials });
        expect(waiting.status()).toBe(403);
        expect((await waiting.json()).title).toBe("Your account is waiting for an administrator's approval.");

        // Professors can't review sign-ups; administrators can.
        expect((await request.get(`${urls.api}/api/accounts/pending`, { headers: await headersFor(request, professor) })).status()).toBe(403);
        const headers = await adminHeaders(request);
        const pending: { id: string; email: string; organization: string }[] =
            await (await request.get(`${urls.api}/api/accounts/pending`, { headers })).json();
        const ada = pending.find(p => p.email === signUp.email)!;
        expect(ada.organization).toBe(signUp.organization);
        expect((await request.post(`${urls.api}/api/accounts/${ada.id}/approve`, { headers })).status()).toBe(204);

        const signedIn = await request.post(`${urls.api}/api/auth/login`, { data: credentials });
        expect(signedIn.status()).toBe(200);
        expect((await signedIn.json()).role).toBe("professor");
    });

    test("rejects incomplete sign-ups field by field", async ({ request }) => {
        const response = await request.post(`${urls.api}/api/auth/signup`, { data: { email: "nope", password: "short" } });
        expect(response.status()).toBe(400);
        expect(Object.keys((await response.json()).errors)).toEqual(expect.arrayContaining([
            "name", "email", "password", "dateOfBirth", "intendedUse", "organization",
        ]));
    });

    test("lets professors manage only the surveys they created", async ({ request }) => {
        const headers = await headersFor(request, professor);
        const demo = await surveyByTitle(request, demoSurveyTitle);
        expect((await request.get(`${urls.api}/api/surveys/${demo.id}/results`, { headers })).status()).toBe(403);
        expect((await request.delete(`${urls.api}/api/surveys/${demo.id}`, { headers })).status()).toBe(403);

        const questions: { id: string; key: string }[] = await (await request.get(`${urls.api}/api/questions`)).json();
        const firstName = questions.find(q => q.key === "first_name")!;
        const created = await request.post(`${urls.api}/api/surveys`, {
            headers, data: { title: `Lab sign-up ${runId}`, description: "Professor's survey", questionIds: [firstName.id] },
        });
        expect(created.status()).toBe(201);
        const survey = await created.json();
        try {
            expect(survey.ownerName).toBe(professor.username);
            expect(survey.status).toBe("draft");
            await publish(request, survey.id, headers);
            const managed: { id: string }[] = await (await request.get(`${urls.api}/api/surveys/managed`, { headers })).json();
            expect(managed.map(s => s.id)).toContain(survey.id);
            expect(managed.map(s => s.id)).not.toContain(demo.id);

            // Students answer without an account; answers sent while signed in record who sent them.
            expect((await request.post(`${urls.api}/api/surveys/${survey.id}/responses`, {
                data: { answers: { first_name: "Ada" } },
            })).status()).toBe(201);
            expect((await request.post(`${urls.api}/api/surveys/${survey.id}/responses`, {
                headers, data: { answers: { first_name: "Trying it out" } },
            })).status()).toBe(201);
            const responses: { submittedBy: string | null }[] =
                await (await request.get(`${urls.api}/api/surveys/${survey.id}/responses`, { headers })).json();
            expect(responses.map(r => r.submittedBy ?? null)).toEqual([null, professor.username]);

            // Administrators can see and manage every survey.
            const adminManaged: { id: string }[] =
                await (await request.get(`${urls.api}/api/surveys/managed`, { headers: await adminHeaders(request) })).json();
            expect(adminManaged.map(s => s.id)).toEqual(expect.arrayContaining([survey.id, demo.id]));
            expect((await request.get(`${urls.api}/api/surveys/${survey.id}/results`, { headers: await adminHeaders(request) })).status()).toBe(200);
        } finally {
            expect((await request.delete(`${urls.api}/api/surveys/${survey.id}`, { headers })).status()).toBe(204);
        }
    });

    test("rejects invalid questions and protects questions in use", async ({ request }) => {
        const headers = await adminHeaders(request);
        const invalid = await request.post(`${urls.api}/api/questions`, { headers, data: { key: "", label: "", type: "weird" } });
        expect(invalid.status()).toBe(400);

        const conditionalOnText = await request.post(`${urls.api}/api/questions`, {
            headers,
            data: { key: `x_${runId}`, label: "X", type: "text", visibleIf: { key: "first_name", shouldEqual: true } },
        });
        expect(conditionalOnText.status()).toBe(400);

        const questions: { id: string; key: string }[] = await (await request.get(`${urls.api}/api/questions`)).json();
        const isStudent = questions.find(q => q.key === "is_student")!;
        const rename = await request.put(`${urls.api}/api/questions/${isStudent.id}`, {
            headers,
            data: { key: "student", label: "Student?", type: "yes_no", required: true },
        });
        expect(rename.status()).toBe(409);
        expect((await request.delete(`${urls.api}/api/questions/${isStudent.id}`, { headers })).status()).toBe(409);
    });

    test("returns problem details keyed by question for bad answers", async ({ request }) => {
        const survey = await surveyByTitle(request, demoSurveyTitle);
        const response = await request.post(`${urls.api}/api/surveys/${survey.id}/responses`, {
            data: { answers: { is_student: "maybe", age: "old", unknown_key: 1 } },
        });
        expect(response.status()).toBe(400);
        const body = await response.json();
        expect(body.title).toBe("One or more validation errors occurred.");
        expect(Object.keys(body.errors)).toEqual(expect.arrayContaining(["is_student", "age", "unknown_key", "first_name"]));
    });

    test("escapes spreadsheet formulas in the CSV export", async ({ request }) => {
        const headers = await adminHeaders(request);
        const questions: { id: string; key: string }[] = await (await request.get(`${urls.api}/api/questions`)).json();
        const firstName = questions.find(q => q.key === "first_name")!;
        const created = await request.post(`${urls.api}/api/surveys`, {
            headers,
            data: { title: `CSV check ${runId}`, description: "Formula escaping", questionIds: [firstName.id] },
        });
        expect(created.status()).toBe(201);
        const survey = await created.json();
        try {
            await publish(request, survey.id, headers);
            expect((await request.post(`${urls.api}/api/surveys/${survey.id}/responses`, {
                data: { answers: { first_name: "=HYPERLINK(\"http://example.com\")" } },
            })).status()).toBe(201);
            const csv = await (await request.get(`${urls.api}/api/surveys/${survey.id}/responses/export`, { headers })).text();
            expect(csv).toContain(`"'=HYPERLINK(""http://example.com"")"`);
        } finally {
            expect((await request.delete(`${urls.api}/api/surveys/${survey.id}`, { headers })).status()).toBe(204);
        }
    });

    test("shares a survey by link, takes one answer per browser, and closes it", async ({ request }) => {
        const headers = await headersFor(request, professor);
        const questions: { id: string; key: string }[] = await (await request.get(`${urls.api}/api/questions`)).json();
        const created = await request.post(`${urls.api}/api/surveys`, {
            headers, data: { title: `Shared ${runId}`, description: "By link", questionIds: [questions.find(q => q.key === "first_name")!.id] },
        });
        const survey = await created.json();
        const answer = (respondentId: string) => request.post(`${urls.api}/api/surveys/${survey.id}/responses`, {
            data: { answers: { first_name: "Ada" }, respondentId },
        });
        try {
            // A draft is private to its owner.
            expect((await request.get(`${urls.api}/api/share/${survey.shareCode}`)).status()).toBe(404);
            expect((await answer("browser-a")).status()).toBe(404);

            // Published without listing: reachable by its link, missing from the public list.
            await publish(request, survey.id, headers, { listed: false });
            const shared = await request.get(`${urls.api}/api/share/${survey.shareCode.toUpperCase()}`);
            expect(shared.status()).toBe(200);
            expect((await shared.json()).id).toBe(survey.id);
            const listed: { id: string }[] = await (await request.get(`${urls.api}/api/surveys`)).json();
            expect(listed.map(s => s.id)).not.toContain(survey.id);

            expect((await answer("browser-a")).status()).toBe(201);
            const again = await answer("browser-a");
            expect(again.status()).toBe(409);
            expect((await again.json()).error).toBe("You've already answered this survey. Thank you!");
            expect(await (await request.get(`${urls.api}/api/surveys/${survey.id}/answered?respondentId=browser-a`)).json())
                .toEqual({ answered: true });
            expect((await answer("browser-b")).status()).toBe(201);

            await publish(request, survey.id, headers, { closesAt: new Date(Date.now() - 60_000).toISOString() });
            const closed = await answer("browser-c");
            expect(closed.status()).toBe(409);
            expect((await closed.json()).error).toBe("This survey is closed and no longer takes answers.");
        } finally {
            expect((await request.delete(`${urls.api}/api/surveys/${survey.id}`, { headers })).status()).toBe(204);
        }
    });
});
