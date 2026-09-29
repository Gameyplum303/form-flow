# **SurveyDefinition Model Documentation**

## Overview

`SurveyDefinition` represents the structure of a survey stored in LiteDB and exchanged through JSON.

It defines the survey’s metadata and the ordered list of questions it contains.

This model is used by:

* The admin pages (creating, editing, previewing and deleting surveys, and viewing results)
* The backend API (loading & saving surveys)
* The Blazor and React clients (displaying surveys to respondents)
* Unit tests validating schema alignment

---

## **C# Model Definition**

```csharp
using LiteDB;

namespace FormFlow.Data.Models
{
    public class SurveyDefinition
    {
        [BsonId]
        public Guid Id { get; set; }

        public required string Title { get; set; }
        public required string Description { get; set; }
        public required List<Guid> QuestionIds { get; set; }
        public required DateTime CreatedAt { get; set; }
        public Guid? OwnerId { get; set; }
        public string? OwnerName { get; set; }

        // Sharing
        public string Status { get; set; } = SurveyStatuses.Published; // "draft" or "published"
        public bool Listed { get; set; } = true;
        public string? ShareCode { get; set; }
        public DateTime? ClosesAt { get; set; }
    }
}
```

### Key Notes

* **Id** is the LiteDB primary key.
* **QuestionIds** preserves question order — the order of GUIDs is the order questions appear in the survey.
* All fields are **required** and enforced by C# 11 `required` properties.
* Clients create and update surveys with the `NewSurvey` body (`title`, `description`, `questionIds`). The API sets `id` and `createdAt`. See [api.md](api.md#surveys).
* Missing required fields during JSON deserialization will throw a `JsonException`.
* **Status**, **Listed**, **ShareCode** and **ClosesAt** are the sharing settings. The defaults (published and listed) describe surveys stored before sharing existed; the API creates new surveys as unlisted drafts. See [api.md](api.md#sharing).

---

## **JSON → C# Property Mapping**

| JSON Field      | C# Property     | Type           | Required | Notes                        |
| --------------- | --------------- | -------------- | -------- | ---------------------------- |
| `id`          | `Id`          | `Guid`       | Yes      | LiteDB document ID           |
| `title`       | `Title`       | `string`     | Yes      | Survey title                 |
| `description` | `Description` | `string`     | Yes      | Survey description           |
| `questionIds` | `QuestionIds` | `List<Guid>` | Yes      | Ordered list of question IDs |
| `createdAt`   | `CreatedAt`   | `DateTime`   | Yes      | ISO 8601 timestamp           |

### Casing Behavior

* JSON uses  **camelCase** .
* C# uses  **PascalCase** .
* Deserialization succeeds because we enable:
  ```csharp
  PropertyNameCaseInsensitive = true
  ```

---

## **Deserializing a Survey**

To deserialize JSON into a `SurveyDefinition`, use:

```csharp
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

var survey = JsonSerializer.Deserialize<SurveyDefinition>(jsonString, options);
```

### Why this is required

* Your JSON schema uses camelCase (`questionIds`, `createdAt`)
* Your C# model uses PascalCase (`QuestionIds`, `CreatedAt`)
* Without case-insensitive matching, System.Text.Json will throw a `JsonException` for missing required fields.

---

## **Validation Behavior**

### Required fields

Because the model uses C# 11 `required` properties:

```csharp
public required string Title { get; set; }
```

System.Text.Json enforces these at deserialization time.

If any required field is missing, the following occurs:

* Deserialization fails immediately
* A `JsonException` is thrown
* The exception message lists missing properties

Example:

```
JSON deserialization for type 'SurveyDefinition' was missing required properties including: 'Title', 'Description', 'QuestionIds', 'CreatedAt'.
```

---

## **Example Valid Survey JSON**

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "title": "Customer Feedback",
  "description": "A simple survey.",
  "questionIds": [
    "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
  ],
  "createdAt": "2024-01-01T12:00:00Z"
}
```

---
