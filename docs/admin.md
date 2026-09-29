# Admin Pages

The admin area is part of the Blazor app and uses its own layout and menu (`AdminNavMenu`). Open it from **Admin Dashboard** in the main menu. There is no sign-in yet, so anyone who can reach the Blazor app can use it.

| Route | Page | Purpose |
|---|---|---|
| `/admin/surveys` | `AdminSurveysList` | All surveys, with Edit, Preview, Results and Delete |
| `/admin/surveys/create` | `AdminCreateSurvey` | Build a new survey |
| `/admin/surveys/{id}/edit` | `AdminCreateSurvey` | Edit an existing survey |
| `/admin/surveys/{id}/preview` | `AdminSurveyPreview` | See the survey as a respondent would, with conditional questions working |
| `/admin/surveys/{id}/results` | `AdminSurveyResults` | Response counts, charts per question, and CSV download |
| `/admin/questions` | `AdminQuestions` | The question bank, with Edit and Delete |
| `/admin/questions/create` | `AdminCreateQuestion` | Create a question |
| `/admin/questions/{id}/edit` | `AdminCreateQuestion` | Edit a question |

**Respondent view** in the admin menu goes back to the public survey list at `/surveys`.

## Question bank (`/admin/questions`)

A table of every question showing its label, key, type, and when it is shown ("Always", or for example "is_student is yes"). Edit opens the question in the editor. Delete asks for confirmation, and the API refuses if a survey uses the question or another question's visibility depends on it; the page shows that message.

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
