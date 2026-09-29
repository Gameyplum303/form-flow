import { expect, test } from "@playwright/test";
import { admin, adminHeaders, demoSurveyTitle, headersFor, professor, runId, student, surveyByTitle, urls } from "./helpers";

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

    test("lets students take surveys but nothing else", async ({ request }) => {
        const survey = await surveyByTitle(request, demoSurveyTitle);
        const headers = await headersFor(request, student);
        expect(await (await request.get(`${urls.api}/api/auth/me`, { headers })).json())
            .toEqual(expect.objectContaining({ username: student.username, role: "student" }));

        expect((await request.get(`${urls.api}/api/surveys/${survey.id}/questions`, { headers })).status()).toBe(200);
        expect((await request.get(`${urls.api}/api/surveys/${survey.id}/results`, { headers })).status()).toBe(403);
        expect((await request.get(`${urls.api}/api/surveys/managed`, { headers })).status()).toBe(403);
        expect((await request.post(`${urls.api}/api/questions`, {
            headers, data: { key: `student_${runId}`, label: "Student", type: "text" },
        })).status()).toBe(403);
        expect((await request.delete(`${urls.api}/api/surveys/${survey.id}`, { headers })).status()).toBe(403);
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
            const managed: { id: string }[] = await (await request.get(`${urls.api}/api/surveys/managed`, { headers })).json();
            expect(managed.map(s => s.id)).toContain(survey.id);
            expect(managed.map(s => s.id)).not.toContain(demo.id);

            // A signed-in student's answers record who sent them.
            expect((await request.post(`${urls.api}/api/surveys/${survey.id}/responses`, {
                headers: await headersFor(request, student), data: { answers: { first_name: "Ada" } },
            })).status()).toBe(201);
            const responses: { submittedBy: string }[] =
                await (await request.get(`${urls.api}/api/surveys/${survey.id}/responses`, { headers })).json();
            expect(responses.map(r => r.submittedBy)).toEqual([student.username]);

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
            expect((await request.post(`${urls.api}/api/surveys/${survey.id}/responses`, {
                data: { answers: { first_name: "=HYPERLINK(\"http://example.com\")" } },
            })).status()).toBe(201);
            const csv = await (await request.get(`${urls.api}/api/surveys/${survey.id}/responses/export`, { headers })).text();
            expect(csv).toContain(`"'=HYPERLINK(""http://example.com"")"`);
        } finally {
            expect((await request.delete(`${urls.api}/api/surveys/${survey.id}`, { headers })).status()).toBe(204);
        }
    });
});
