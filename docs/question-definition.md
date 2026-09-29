# **QuestionDefinition Model Documentation**

This document describes the backend model used to represent a single question within the FormFlow system. The model mirrors the shared JSON schema used by the UI and API, ensuring consistent structure, validation, and behavior across the entire application.

---

## **Purpose of the Model**

The `QuestionDefinition` model provides a strongly typed representation of a form question. It enables:

* Consistent serialization/deserialization between backend and UI
* A unified structure for validation logic
* Predictable rendering behavior in the UI
* Safe storage in LiteDB
* Clear enforcement of required fields defined in the JSON schema

This model is the foundation for question creation, editing, storage, and runtime evaluation.

---

## **Model Overview**

### **QuestionDefinition**

Represents a single question in a dynamic form.

| Property              | Type             | Required           | Description                                           |
| --------------------- | ---------------- | ------------------ | ----------------------------------------------------- |
| `Id`                | `Guid`         | Yes                | Unique identifier for the question.                   |
| `Key`               | `string`       | Yes                | Unique key used to reference the question.            |
| `Label`             | `string`       | Yes                | Human‑readable label shown to the user.              |
| `Type`              | `string`       | Yes                | Input type (e.g., text, number, dropdown).            |
| `Required`          | `bool`         | No (default: true) | Whether the question must be answered.                |
| `Placeholder`       | `string?`      | No                 | Optional placeholder text.                            |
| `DefaultValue`      | `string?`      | No                 | Optional default value.                               |
| `HelpText`          | `string?`      | No                 | Optional guidance shown beneath the question.         |
| `Options`           | `List<Option>` | No                 | Selectable options for dropdown/radio/checkbox types. |
| `VisibleIf`         | `VisibleIf?`   | No                 | Conditional visibility rule.                          |
| `ValidationConfigs` | `string?`      | No                 | JSON‑serialized array of validation rule objects.    |

---

### **Option**

Represents a selectable option for dropdown, radio, checkbox, or multiselect questions.

| Property  | Type       | Required | Description                        |
| --------- | ---------- | -------- | ---------------------------------- |
| `Value` | `string` | Yes      | The submitted value when selected. |
| `Label` | `string` | Yes      | The text shown to the user.        |

---

### **VisibleIf**

Defines a conditional visibility rule for a question.

| Property        | Type       | Required | Description                                  |
| --------------- | ---------- | -------- | -------------------------------------------- |
| `Key`         | `string` | Yes      | The key of the controlling question.         |
| `ShouldEqual` | `bool`   | Yes      | The expected value that triggers visibility. |

This allows questions to appear only when another question’s answer matches a specific boolean value. The controlling question must be a `yes_no` question, and a question can't depend on itself. Rules can chain (C depends on B, which depends on A); a question in a circular chain is treated as hidden.

Answers to hidden questions are dropped when a response is submitted, and hidden questions are never required.

---

## **JSON Schema Alignment**

The model is designed to match the JSON schema exactly:

* Required fields (`id`, `key`, `label`, `type`) are marked with `required` in C#.
* Optional fields are nullable.
* `validationConfigs` remains a  **string** , as defined in the schema.
* `VisibleIf` and `Option` are represented as separate classes.
* JSON property names are preserved using `JsonPropertyName` where needed.

This ensures seamless communication between backend and frontend.

### **ValidationConfigs Internal Structure**

Although `validationConfigs` is stored and transmitted as a  **string** , the contents of that string must follow a well‑defined structure. Internally, the value must be a JSON array of validation rule objects with the following schema:

```
[
  {
    "validationType": "string",          // One of: MinLength, MaxLength, MinValue, MaxValue, Range

    "minLength": "integer (optional)",   // Required only for MinLength
    "maxLength": "integer (optional)",   // Required only for MaxLength

    "minValue": "integer (optional)",    // Required for MinValue or Range
    "maxValue": "integer (optional)",    // Required for MaxValue or Range

    "message": "string (optional)"
  }
]
```


This describes the **expected shape** of the validation rules without including real example data.

The backend validation engine deserializes this string into rule objects and applies the rules accordingly.

## **Serialization & Deserialization**

The model supports round‑trip JSON serialization without data loss:

* All fields map directly to schema properties
* Optional fields are omitted when null
* Lists are initialized to avoid null references
* Nested objects serialize cleanly
* `ValidationConfigs` remains a raw JSON string for flexibility

Unit tests verify that:

* Valid JSON deserializes successfully
* Invalid JSON fails gracefully
* Required fields are enforced
* Visibility and options structures deserialize correctly

---

## **Options Example JSON**

The following examples show valid JSON structures for option-based question types. Options are required for `dropdown`, `radio`, and `multiselect`. A `checkbox` can list options (each one is its own tick box, and several can be ticked) or leave them out to be a single yes/no tick box.

### Dropdown

```json
{
  "id": "a1b2c3d4-0000-0000-0000-000000000001",
  "key": "study_level",
  "label": "Study Level",
  "type": "dropdown",
  "required": true,
  "options": [
    { "label": "Undergraduate", "value": "undergrad" },
    { "label": "Postgraduate", "value": "postgrad" },
    { "label": "PhD", "value": "phd" }
  ]
}
```

### Radio

```json
{
  "id": "a1b2c3d4-0000-0000-0000-000000000002",
  "key": "contact_method",
  "label": "Preferred Contact Method",
  "type": "radio",
  "required": true,
  "options": [
    { "label": "Email", "value": "email" },
    { "label": "Phone", "value": "phone" },
    { "label": "Post", "value": "post" }
  ]
}
```

### Multiselect

```json
{
  "id": "a1b2c3d4-0000-0000-0000-000000000003",
  "key": "skills",
  "label": "Skills",
  "type": "multiselect",
  "required": false,
  "options": [
    { "label": "C#", "value": "csharp" },
    { "label": "JavaScript", "value": "javascript" },
    { "label": "Python", "value": "python" },
    { "label": "SQL", "value": "sql" }
  ]
}
```

### Checkbox

```json
{
  "id": "a1b2c3d4-0000-0000-0000-000000000004",
  "key": "subscribe_newsletter",
  "label": "Subscribe to newsletter",
  "type": "checkbox",
  "required": false,
  "options": [
    { "label": "Yes, subscribe me", "value": "yes" }
  ]
}
```

---

## **Question Types and Answers**

| Type | Rendered as | Answer stored as |
|---|---|---|
| `text` | Text field | The text |
| `number` | Number field | The number as text, e.g. `"28"` |
| `yes_no` | Yes / No radio buttons | `"true"` or `"false"` |
| `dropdown` | Select list | One option value |
| `radio` | Radio buttons | One option value |
| `checkbox` | One tick box, or one per option | `"true"`/`"false"` without options, otherwise the ticked option values |
| `multiselect` | A tick box per option | The selected option values |

`validationConfigs` rules apply to `text` (`MinLength`, `MaxLength`) and `number` (`MinValue`, `MaxValue`, `Range`) answers. The admin create page builds these rules for you.

---

## **Usage in the System**

The `QuestionDefinition` model is used by:

* The API (for sending/receiving question definitions)
* The validation engine (to evaluate rules and required fields)
* The UI (to render dynamic forms)
* The database layer (LiteDB storage)

It serves as the single source of truth for question structure across the entire application.
