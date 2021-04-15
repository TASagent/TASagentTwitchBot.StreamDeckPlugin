# TASagentTwitchBot.StreamDeckPlugin

This project was created from a fork of [streamdeck-client-csharp](https://github.com/TyrenDe/streamdeck-client-csharp).

This adds the ability to conveniently configure the streamdeck to trigger TASagentTwitchBot rest endpoints. Designed to work with the [TASagentTwitchBotCore](https://github.com/TASagent/TASagentTwitchBotCore) project.

## Getting started

Download [the latest Release](https://github.com/TASagent/TASagentTwitchBot.StreamDeckPlugin/releases) (or download and compile this project) and copy the directory `wtf.tas.tasagentbot.sdPlugin` into `%AppData%/Elgato/StreamDeck/Plugins/`. Quit and reopen the StreamDeck software (be sure to quit the software from the tray icon).

## Features

All of the plugins conveniently share some global settings so that you're not forced to re-enter the same data over and over again. By default, you will want to set the `Bot URL` field to `http://localhost:5000` and the `Config File` field to `<YOUR_BOT_DIRECTORY>\Config\Config.json` (for example, mine is `C:\Users\tasag\Documents\TASagentBot\Config\Config.json`).

By making direct use of the local Config file on the host, the bot is able to submit all requests authenticated as Admin.

### Sound Effect

Trigger an immediate sound effect. This sound effect bypasses the notification queue. The value in the `Sound Effect` field should just be a registered sound effect alias.

### Voice Effect

Change the current voice effect. The value in the `Voice Effect` field will be sent to the server and parsed as an Effect when pressed.

### General REST

Submit a general REST request to the bot. When pressed, the value in the `JSON Body` field will be attached to a `POST` request and submitted to the indicated `EndPoint`. A smilie face is present in the bottom right to indicate whether the system can successfully parse the `JSON Body` field as a JSON string.

A few examples of how to use this to do common tasks follow, with the values for the `EndPoint` and `JSON Body` fields given, in order.

To trigger the Notification Skip endpoint at `http://localhost:5000/TASagentBotAPI/Notifications/Skip`, use an `EndPoint` of `/TASagentBotAPI/Notifications/Skip` and a `JSON Body` of `{}`.  
To replay the last notificaiton, use `/TASagentBotAPI/Notifications/ReplayNotification` and `{ "Index": -1 }`

To start the overlay timer, use `/TASagentBotAPI/Timer/Start` and `{}`.  
To stop the overlay timer, use `/TASagentBotAPI/Timer/Stop` and `{}`.  
To reset the overlay timer, use `/TASagentBotAPI/Timer/Reset` and `{}`.  
To mark a lap for the overlay timer, use `/TASagentBotAPI/Timer/MarkLap` and `{}`.  
To unmark a lap for the overlay timer, use `/TASagentBotAPI/Timer/UnmarkLap` and `{}`.  
To reset the current lap for the overlay timer, use `/TASagentBotAPI/Timer/ResetCurrentLap` and `{}`.  
To save the current timer under the name "Invictus", use `/TASagentBotAPI/Timer/SaveTimer` and `{ "TimerName": "Invictus" }`.  
To load a saved timer named "Invictus", use `/TASagentBotAPI/Timer/LoadTimer` and `{ "TimerName": "Invictus" }`.  

To create and play a TTS message with the voice of "Brian", with normal pitch, as the user "TASagent", with no special effect, and saying "rrrrr", use `/TASagentBotAPI/TTS/Play` and
```json
{
    "Voice": "Brian",
    "Pitch": "normal",
    "Effect": "",
    "Text": "rrrrr",
    "User": "TASagent"
}
```

### Mic Monitor

The Mic Monitor button indicates the current state of Voice effects applied to the microphone. It has a Green background when the mic has no special effects, a Red background when a special effect is applied, and a background split between Green and Red when it cannot contact the bot. Pressing this button removes all current mic effects.

### Lockdown Monitor

The Lockdown Monitor button indicates the current state of the Control Page Lockdown. It has a Green background and a closed lock when the ControlPage is accessible only to administrators, it has a Red background and an open lock when the ControlPage is accessible to all levels of authentication, and it has a backgroud split between green and red when it cannot contact the bot. Pressing this button toggles the current Lockdown status, and initiating Lockdown mode generates new authentication keys.