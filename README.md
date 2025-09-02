# Custom Manhattan SCALE API

This project provides a custom API for Manhattan SCALE, allowing user input from a dialog box (like the "update priority" in Work Insight) to be passed alongside an internal ID to identify the record(s) to update.

## Features

This project lets you easily add custom actions to your Manhattan SCALE web app. With this tool, you can:
- Edit any table in SCALE using simple user input from a dialog box—no coding required for each new action!
- Works directly with your existing SCALE web app and dialogs.
- Fully modular: after setup, adding new actions is easy—just write the SQL you want to run.
- No need to create a new API for every custom action—one setup covers all your needs.

## How It Works

When you call the API, it always passes three parameters to the stored procedure `usp_UserAction`:
- `@action` – the name of the action you want to perform (e.g., "UpdatePriority")
- `@internalID` – the record or row you want to affect
- `@changeValue` – the new value or input from the user

Inside `usp_UserAction`, you can use the value of `@action` to branch your logic and perform different updates or actions based on what the user selected. This makes it easy to add new buttons or actions in SCALE—just add a new branch in your stored procedure!

## Setup Instructions

### 1. Clone and Publish
- Clone this repository.
- Publish the project to a folder on your SCALE application server.

### 2. Edit web.config Before Publishing
- Open the `web.config` file in your published folder (or add one if it doesn't exist).
- Add the following configurations:
  ```xml
  <configuration>
    <connectionStrings>
      <add name="DefaultConnection" connectionString="Server=YOUR_SERVER_HERE;Database=YOUR_DATABASE_HERE;User Id=YOUR_DB_USER_HERE;Password=YOUR_DB_PASSWORD_HERE;TrustServerCertificate=True;" />
    </connectionStrings>
    <appSettings>
      <add key="ApiKey" value="YOUR_API_KEY_HERE" />
    </appSettings>
  </configuration>
  ```
- Replace the placeholders with your actual database connection details and API key.

### 3. IIS Configuration
- Create a new Application Pool in IIS (recommended: use a dedicated service account).
- Add the published folder as an Application under the same IIS site as your SCALE site.
  - I named mine UserAction

### 4. Recycle the App Pool
- In IIS, recycle the UserAction Application Pool to apply changes.

### 5. Integrate with Manhattan SCALE (Snapdragon)
- Update your dialog's save button click event to use:
  - **Event name:** `_webUi.insightListPaneActions.modalDialogPerformPostForSelection`
  - **Parameters:**
    - `POSTServiceURL=/UserAction/ExecProc?action=ExampleAction`
    - `PostData_Grid_ListPaneDataGrid_internalID=internal_num_example`
    - `PostData_Input_ExampleEditor_changeValue=value`
    - `ModalDialogName=ExampleModalDialog`
