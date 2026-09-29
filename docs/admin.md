# Admin Pages

The admin area is part of the Blazor app. On any page under `/admin`, the sidebar (`NavMenu`) switches to the admin links. Open it from **Survey Builder** in the main menu, or sign in.

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

Professors and scientists sign up at `/signup` (linked from the home page, the menu and the sign-in page) with their name, email, password, date of birth, organization and how they'll use FormFlow. The page checks the same rules as the API before sending, and shows each problem under its field. The API emails them a link to verify their address, and the account then waits for approval. Signing in before either step says which one is missing; before verifying, the sign-in page also offers **Email me a new link**. Administrators see a **Sign-ups** link in the admin menu, which opens `/admin/signups`, a table of waiting sign-ups showing whether each email is verified, with **Approve** and **Decline** (which asks first, then deletes the sign-up). Set `SignUp:RequireApproval` to `false` on the API to let new professors sign in without approval, and `SignUp:RequireEmailVerification` to `false` to skip the email step.

### Passwords and email

- **Forgot your password?** on the sign-in page opens `/forgot-password`, which emails a reset link. The link opens `/reset-password`, lasts one hour and works once. Resetting signs the account out everywhere.
- **Change password** under the account's name in the menu opens `/account`. It asks for the current password, and signs the account out on every other browser.
- Verification links open `/verify-email`, which says whether the account can sign in now or still waits for approval.

Without an SMTP server (see [backend.md](backend.md#configuration)), emails aren't sent anywhere. Administrators read them on the **Emails** page (`/admin/outbox`), with the links clickable, which is how the demo and the browser tests follow them. With `Email:Smtp:Host` set, emails go out for real and the page says there is no outbox.

### Viewing the site as another role

An administrator's menu has a **View as** list under their name. Choosing Professor/Scientist or Student shows the site as that role would see it, with a banner and a **Back to Administrator view** button on every page. As a professor, an administrator sees only the surveys and questions they created themselves. The choice lasts for the tab, across page loads, and signing out clears it. It only changes what the pages show: the API still treats the account as an administrator.

The API issues the token (see [api.md](api.md#authentication)). `AdminSession` keeps it in the browser's session storage, encrypted with ASP.NET Core data protection, so reloading a page keeps the account signed in and closing the tab signs it out. `QuestionService` and `SurveyService` add it to every API call. **Sign out** in either menu clears it, along with any View as choice.

| Route | Page | Purpose |
|---|---|---|
| `/login` | `Login` | Sign in with a username or email |
| `/signup` | `SignUp` | Professor/scientist sign-up |
| `/admin/signups` | `AdminSignUps` | Approve or decline sign-ups (administrators only) |
| `/forgot-password` | `ForgotPassword` | Ask for a password reset link |
| `/reset-password?token=…` | `ResetPassword` | Choose a new password from an emailed link |
| `/verify-email?token=…` | `VerifyEmail` | Verify an email address from an emailed link |
| `/account` | `AccountSettings` | Change your password (signed in) |
| `/admin/outbox` | `AdminOutbox` | Emails the API would have sent, when no SMTP server is set (administrators only) |
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
| Type | One of the fourteen types. Choosing dropdown, radio, checkbox or multiselect shows the options editor; a likert grid shows a rows editor and a scale editor. |
| Required | Whether respondents must answer. |
| Placeholder, Default Value, Help Text | Optional. |
| Options | Label and value pairs, added and removed with the buttons. Needed for dropdown, radio and multiselect; optional for checkbox. |
| Rows and Scale | For a likert grid: the statements people rate (label and value; a value can't contain `=`), and the scale's columns. Leave the scale empty for Strongly disagree (1) to Strongly agree (5). |
| Validation | Minimum and maximum length for text questions, minimum and maximum value for number questions, the number of stars for a rating, and a slider's range (0 to 100 when left empty). Saved as the question's `validationConfigs`. |
| Only show when | Pick a yes/no question and Yes or No to make this question conditional. |

The API checks the question again when it is saved and any errors are shown on the page. After an edit the page returns to the question bank.

## Surveys (`/admin/surveys`)

A table of surveys with their question counts and sharing status: **Draft**, **Published** (on the public list), **Published, link only**, or **Closed**. **Share** opens the survey's Share page. Delete asks for confirmation and also deletes the survey's responses.

New surveys are drafts, so nobody else can open them until they're published.

## Share (`/admin/surveys/{id}/share`)

Controls who can answer a survey:

- **Draft** or **Published**. A published survey can be answered by anyone with the link, without an account.
- **Also show it on the public list of surveys**. Leave it off to share the survey only by its link.
- **Stop taking answers at**, in the browser's local time. After that the survey drops off the public list and shows a "closed" message. **Clear** removes the close date.

**Save sharing settings** applies them. The right side shows the share link (`/s/{code}`) with **Copy link** and **Open**, and a QR code of the link for students to scan. Each browser can answer a survey once; coming back shows a thank-you instead of the form.

## Create or edit a survey

Enter a title and description, then add questions from the question bank and put them in order with the up and down buttons. The page warns when a conditional question is in the survey without the question it depends on (it would never show), or appears before it. Save is enabled once the title, description and at least one question are filled in.

## Preview (`/admin/surveys/{id}/preview`)

Renders the survey with the same `SurveyForm` component respondents use, so conditional questions can be tried out. Nothing is submitted.

## Results (`/admin/surveys/{id}/results`)

Shows the number of responses and when the latest arrived, then a card per question:

- choice and yes/no questions: a bar per option with count and percentage,
- rating questions: the average rating, then a bar per star,
- NPS questions: the Net Promoter Score (the percentage of promoters, 9 or 10, minus the percentage of detractors, 0 to 6) with the number of promoters, passives and detractors, then a bar per score,
- likert grids: a table with a row per statement, the count and percentage for each point of the scale, and each statement's average when the scale's values are numbers,
- number and slider questions: average, minimum and maximum,
- text questions: the five most recent answers.

**Explore the responses** narrows and splits them:

- **Add a filter** lists every yes/no, choice, rating and NPS question with its answers. Picking one keeps only the responses that gave that answer; each filter shows as a chip with a remove button, and several filters must all match. The page then says how many of the responses match.
- **Sent from** and **to** keep responses sent between those days, in the browser's time zone, including the whole of the last day.
- **Compare groups by** picks a question to split by. Each choice, rating, NPS, number, slider and likert question then gets a table with a column per answer (and "No answer" when some skipped it), giving counts with the percentage of that group who answered, averages, each group's NPS, each statement's average, and how many answered.
- **Clear all** removes filters, dates and the comparison.

Above the question cards, a bar chart shows how many matching responses arrived each day (each week or month for longer ranges), in local time. Hover a bar for its date and count.

**Download CSV** calls the API's export endpoint with the same filters and dates, so the file has exactly the matching responses. **Share** goes to the survey's Share page.
