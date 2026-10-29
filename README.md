# Day Tasks

**A simple daily to-do list that lives on your desktop and in your pocket.** A small always-at-hand widget
for Windows and an app for Android, with your tasks synced between them.

[Українська версія](README.uk.md)

## Features

- **Tasks by day.** Plan today, tomorrow or any date in the calendar; days with tasks are marked in it.
- **Nothing gets lost.** One click moves all unfinished tasks from past days to today.
  A single task can be moved to tomorrow or any other day.
- **Quick editing.** Double-click (double-tap on a phone) to edit a task, drag to reorder,
  *Alt + ← / →* to switch days.
- **Levels and achievements.** Earn XP for finished tasks, keep up a daily streak,
  see a 17-week activity map and unlock 17 achievements.
- **Sync between PC and phone.** Sign in with the same account on both, and changes show up on the other device.
  Works offline too: changes are sent as soon as you're back online.
- **Widget for Windows.** A small frameless window. It can stay on top of other windows and remembers its position.
- **Light and dark themes** (or follow the system), **English and Ukrainian** interface.
- **Automatic updates.** When a new version is out, the app offers to install it in one click.

## Download

| Device | File | Requirements |
|---|---|---|
| Windows | [TaskWidget.exe](https://github.com/DimaN221221/taskwidget/releases/latest/download/TaskWidget.exe) | Windows 10 / 11 |
| Android | [DayTasks.apk](https://github.com/DimaN221221/taskwidget/releases/latest/download/DayTasks.apk) | Android 8 or newer |

All versions and their change notes are on the [Releases](https://github.com/DimaN221221/taskwidget/releases) page.

## Installing

**Windows.** Put `TaskWidget.exe` in any folder you like (for example, *Documents*) and run it. Nothing else is needed.
If Windows shows *“Windows protected your PC”*, click **More info → Run anyway**: the app is not signed with a paid
certificate, which is why the warning appears.

**Android.** Open `DayTasks.apk` on your phone. If Android asks, allow installing apps from this source
(your browser or file manager), then tap **Install**. The app appears as **Tasks**.

## Syncing your PC and phone

1. Tap the cloud button in the top bar.
2. Choose **Create account**, enter your email and a password (at least 6 characters).
3. Open the confirmation email and follow the link in it.
4. Sign in with the same email and password on the other device.

That's it: tasks are synced on launch, after every change and every minute. The cloud button shows when the last sync happened.
You don't need an account to use the app — without one, tasks are just kept on the device.

## Updates

A few seconds after launch the app checks for a new version. If there is one, a bar appears at the bottom:
*“Version X is available — Update”*.

- **Windows:** the widget downloads the new version, restarts and keeps all your tasks.
- **Android:** the app downloads the new version and opens the system installer. The first time,
  Android asks you to allow **Tasks** to install apps: turn it on, come back and tap **Update** again.

## Where your tasks are kept

- **Windows:** `%APPDATA%\TaskWidget` (the tasks and the window position).
- **Android:** inside the app's own storage.
- **Cloud** (only if you signed in): each account sees only its own tasks.

## Uninstalling

- **Windows:** close the widget and delete `TaskWidget.exe`. To remove your data as well, delete the folders
  `%APPDATA%\TaskWidget` and `%LOCALAPPDATA%\TaskWidget`.
- **Android:** uninstall **Tasks** like any other app.

---

Building the app yourself or curious how it works? See [DEVELOPMENT.md](DEVELOPMENT.md) (in Ukrainian).
