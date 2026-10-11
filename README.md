# Tether

Tether turns your phone into a wireless controller for your laptop. Use it as a
mouse, a keyboard, or a game controller, with no extra hardware.

## How it works

The laptop runs a desktop app built with Avalonia (C#) that hosts a WebSocket
server. The phone connects to it, and the input you make on the phone (touch
movement, taps, key presses, game controls) is sent to the laptop in real time.

## Features

- **Mouse**: use your phone screen as a touchpad
- **Keyboard**: type on your laptop from your phone
- **Game controller**: use your phone as a gamepad
- **Wireless**: phone and laptop communicate over WebSocket

## Tech stack

- Desktop app: C#, Avalonia
- Communication: WebSocket

## Status

Final year project, in development.
