# Troubleshooting

**The API returns an empty list, or old data**

The seeder only adds sample data to an empty database. Stop the API, delete `FormFlow.Backend/formflow.db`, and start it again.

**The Blazor app shows errors loading questions or surveys**

The Blazor app calls the API at `BackendApi:BaseUrl` in `FormFlow.Blazor/appsettings.json`, which defaults to `https://localhost:7209/`. Make sure the API is running (`dotnet run` in `FormFlow.Backend`) and that the development certificate is trusted:

```bash
dotnet dev-certs https --trust
```

To use HTTP instead, run the Blazor app with `dotnet run --BackendApi:BaseUrl=http://localhost:5164/`.

**The React app says it can't reach the API**

It calls `http://localhost:5164` by default. Start the API, or set `REACT_APP_API_URL` before `npm start` if the API is somewhere else.

**The browser warns "Your connection is not private" on localhost**

Run `dotnet dev-certs https --trust` and restart the browser, or use the HTTP URLs.

**A port is already in use**

Pick another port:

```bash
dotnet run --urls "http://localhost:5200"   # API or Blazor
PORT=3001 npm start                          # React, macOS/Linux
set PORT=3001 && npm start                   # React, Windows CMD
```

If you move the API, point the clients at it as described above.

**`dotnet run` says it can't find a project**

Run it inside `FormFlow.Backend` or `FormFlow.Blazor`, or pass the project: `dotnet run --project FormFlow.Backend`.

**`npm ci` fails with "missing from lock file"**

The lock file is out of date. Run `npm install` in that folder and commit the updated `package-lock.json`.
