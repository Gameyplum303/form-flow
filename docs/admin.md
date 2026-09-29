# Admin Pages

The admin area is part of the Blazor app and uses its own layout and menu (`AdminNavMenu`). Open it from **Admin Dashboard** in the main menu.

## Signing in

Every `/admin` page needs a sign-in. `AdminGuard` (in `MainLayout`) sends a signed-out visitor to `/login?returnUrl=…` and brings them back to the page they asked for once they sign in. Development and docker compose create two accounts, both with the password `password`: `Rogers` (Administrator) and `professor` (Professor/Scientist). The sign-in page shows the test account when the `LoginHint` setting is set. Professors and scientists who sign up sign in with their email address.

## Roles

| Role | In the Blazor app |
|---|---|
| Administrator | Every admin page, with Edit and Delete on every question and survey, a **Created by** column, and results for every survey. Can switch to another role's view (below). |
| Professor/Scientist | The survey builder, showing only the surveys they created. The question bank lists every question, so any of them can go in a survey, but Edit and Delete appear only on their own. Opening someone else's survey or question for editing shows "You can only edit…" instead of the form. |
| Student | Takes surveys without an account. An administrator can preview this view with **View as**: the menu has no **Survey Builder** link, and `/admin` pages say that students can't open the survey builder. |

The API enforces the same rules (see [api.md](api.md#authentication)), so hiding a button is only a convenience. Responses sent while signed in, for example a professor trying out their own survey, record the username in `submittedBy`, which shows in the CSV export. How surveys will be shared with students isn't decided yet, so every survey is still listed for everyone.

### Signing up and approval

Professors and scientists sign up at `/signup` (linked from the home page, the menu and the sign-in page) with their name, email, password, date of birth, organization and how they'll use FormFlow. The page checks the same rules as the API before sending, and shows each problem under its field. A new account waits for approval: signing in with it says so. Administrators see a **Sign-ups** link in the admin menu, which opens `/admin/signups`, a table of waiting sign-ups with **Approve** and **Decline** (which asks first, then deletes the sign-up). Set `SignUp:RequireApproval` to `false` on the API to let new professors sign in straight away.

### Viewing the site as another role

An administrator's menu has a **View as** list under their name. Choosing Professor/Scientist or Student shows the site as that role would see it, with a banner and a **Back to Administrator view** button on every page. As a professor, an administrator sees only the surveys and questions they created themselves. The choice lasts for the tab, across page loads, and signing out clears it. It only changes what the pages show: the API still treats the account as an administrator.

The API issues the token (see [api.md](api.md#authentication)). `AdminSession` keeps it in the browser's session storage, encrypted with ASP.NET Core data protection, so reloading a page keeps the account signed in and closing the tab signs it out. `QuestionService` and `SurveyService` add it to every API call. **Sign out** in either menu clears it, along with any View as choice.

| Route | Page | Purpose |
|---|---|---|
| `/login` | `Login` | Sign in with a username or email |
| `/signup` | `SignUp` | Professor/scientist sign-up |
| `/admin/signups` | `AdminSignUps` | Approve or decline sign-ups (administrators only) |
| `/admin/surveys` | `AdminSurveysList` | The surveys you manage (every survey for an administrator), with Edit, Preview, Results and Delete |
| `/admin/surveys/create` | `AdminCreateSurvey` | Build a new survey |
| `/admin/surveys/{id}/edit` | `AdminCreateSurvey` | Edit an existing survey |
| `/admin/surveys/{id}/preview` | `AdminSurveyPreview` | See the survey as a respondent would, with conditional questions working |
| `/admin/surveys/{id}/results` | `AdminSurveyResults` | Response counts, charts per question, and CSV download |
| `/admin/questions` | `AdminQuestions` | The question bank, with Edit and Delete |
| `/admin/questions/create` | `AdminCreateQuestion` | Create a question |
| `/admin/questions/{id}/edit` | `AdminCreateQuestion` | Edit a question |

**Take a Survey** in the admin menu goes back to the public survey list at `/surveys`.

## Question bank (`/admin/questions`)

A table of every question showing its label, key, type, when it is shown ("Always", or for example "is_student is yes"), and who created it. Edit opens the question in the editor. Delete asks for confirmation, and the API refuses if a survey uses the question or another question's visibility depends on it; the page shows that message.

## Create or edit a question

| Field | Notes |
|---|---|
| Label | Required. The question text respondents see. |
| Key | Required, unique. Used in answers, visibility rules and CSV columns, for example `favorite_language`. A key can't be changed once a survey or rule uses it. |
| Type | One of the seven types. Choosing dropdown, radio, checkbox or multiselect shows the options editor. |
| Required | Whether respondents must answer. |
| Placeholder, Default Value, Help Text | Optional. |
| Options | Label and value pairs, added and removed with the buttons. Needed for dropdown, radio and multiselect; optional for checkbox. |
| Validation | Minimum and maximum length for text questions, minimum and maximum value for number questions. Saved as the question's `validationConfigs`. |
| Only show when | Pick a yes/no question and Yes or No to make this question conditional. |

The API checks the question again when it is saved and any errors are shown on the page. After an edit the page returns to the question bank.

## Surveys (`/admin/surveys`)

A table of surveys with their question counts. Delete asks for confirmation and also deletes the survey's responses.

## Create or edit a survey

Enter a title and description, then add questions from the question bank and put them in order with the up and down buttons. The page warns when a conditional question is in the survey without the question it depends on (it would never show), or appears before it. Save is enabled once the title, description and at least one question are filled in.

## Preview (`/admin/surveys/{id}/preview`)

Renders the survey with the same `SurveyForm` component respondents use, so conditional questions can be tried out. Nothing is submitted.

## Results (`/admin/surveys/{id}/results`)

Shows the number of responses and when the latest arrived, then a card per question:

- choice and yes/no questions: a bar per option with count and percentage,
- number questions: average, minimum and maximum,
- text questions: the five most recent answers.

**Download CSV** calls the API's export endpoint. **Open survey** goes to the respondent page.
