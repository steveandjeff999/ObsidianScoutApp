# Quick Start Guide - OBSIDIAN Scout Mobile App

## First Time Setup

### 1. Configure Server Connection

When you first launch the app, you'll see the login screen. Before logging in, you need to configure your server connection:

1. Tap the "?? Server Configuration" button
2. Configure the following:
   - **Protocol**: Select `http` or `https`
     - Use `https` for production servers
     - Use `http` for local development
   - **Server Address**: Enter your server's IP or domain
     - Example IP: `192.168.1.100`
     - Example Domain: `scout.team5454.com`
   - **Port**: Enter the port number
     - Common ports: `443` (HTTPS), `8080` (HTTP), `3000` (dev)
3. Review the "Full URL" preview to confirm it's correct
4. Tap "Test Connection" to verify the server is reachable
5. Tap "Save" to store the configuration

### 2. Login

1. Enter your username (provided by your team admin)
2. Enter your password
3. **Enter your team number** (e.g., `5454`)
4. Tap "Login"

### 3. Start Scouting

Once logged in, you'll see the home screen with three main options:

#### Scout Match
1. Tap "?? Scout Match"
2. Enter the Team ID you're scouting
3. Enter the Match ID
4. Record performance data:
   - **Autonomous**: Speaker notes and Amp notes scored
   - **Teleop**: Speaker notes and Amp notes scored
   - **Endgame**: Climb status (none, attempted, successful, failed)
   - **Notes**: Add any additional observations
5. Tap "Submit Scouting Data"
6. Use "Reset Form" to clear and start a new entry

#### View Teams
1. Tap "?? View Teams"
2. Browse the list of teams at your event
3. Pull down to refresh the list
4. Tap a team to view details (coming soon)

#### View Events
1. Tap "?? View Events"
2. See all events your team is participating in
3. Pull down to refresh
4. Tap an event to view matches (coming soon)

## Common Server Configurations

### Local Development Server
```
Protocol: http
Address: localhost
Port: 8080
```
or
```
Protocol: http
Address: 192.168.1.100
Port: 8080
```

### Cloud-Hosted Production Server
```
Protocol: https
Address: scout.yourteam.com
Port: 443
```

### Server on Custom Port
```
Protocol: https
Address: 10.0.0.100
Port: 3000
```

## Login Credentials

Your team admin will provide you with three pieces of information:
- **Username**: Your unique scout username (e.g., `scout_john`)
- **Password**: Your secure password
- **Team Number**: Your FRC team number (e.g., `5454`)

?? **Important**: The team number must match your team's official FRC team number for the system to work correctly.

## Tips for Effective Scouting

1. **Before the Event**
   - Test your connection to the server
   - Ensure you have valid login credentials (including team number)
   - Familiarize yourself with the scouting form

2. **During Matches**
   - Write down the Team ID and Match ID before the match starts
   - Focus on one robot at a time
   - Use the counter buttons (+/-) for quick tallying
   - Add detailed notes about strategy and performance

3. **After Each Match**
   - Submit data immediately while it's fresh
   - Double-check Team ID and Match ID before submitting
   - Use the "Reset Form" button for the next match

4. **Managing Connectivity**
   - The app works best with continuous internet connection
   - If connection is lost, data may fail to submit
   - Offline sync support coming in future updates

## Troubleshooting Quick Fixes

### "Connection failed" Error
- Check if server URL is correct
- Verify you're on the correct network
- Make sure server is running
- Try the "Test Connection" button

### "Invalid credentials" Error
- Verify username, password, and team number are correct
- Check with your team admin for correct credentials
- Ensure you're connecting to the correct server
- **Make sure your team number is correct** (common issue!)

### "Please enter team number" Error
- The team number field is required for login
- Enter your FRC team number (e.g., `5454`)
- Contact your team admin if you don't know your team number

### App Won't Submit Scouting Data
- Check internet connection
- Verify authentication hasn't expired (7-day token)
- Try logging out and back in
- Check Team ID and Match ID are valid numbers

### Settings Not Saving
- Make sure you tap "Save" after entering server details
- Check that all fields are filled out correctly
- Port must be a number between 1-65535

## Keyboard Shortcuts (Desktop/Tablet)

- **Tab**: Move between form fields
- **Enter**: Submit forms
- **Esc**: Close dialogs

## Best Practices

? **DO:**
- Test your connection before the event starts
- Submit data immediately after each match
- Add meaningful notes about robot performance
- Keep your login credentials secure
- **Remember your team number** - you'll need it to login

? **DON'T:**
- Share your login credentials with others
- Submit fake or inaccurate data
- Scout multiple robots in a single form (one robot per submission)
- Forget to log out when using a shared device
- Use the wrong team number (this will cause authentication issues)

## Getting Help

If you encounter issues:

1. **Check the README.md** for detailed documentation
2. **Test your connection** using the built-in test button
3. **Contact your team's scouting lead** for help
4. **Check server logs** if you have admin access
5. **Verify your team number** with your team admin

## Privacy & Security

- Your password is never stored on the device
- Authentication tokens expire after 7 days
- All data is encrypted during transmission (when using HTTPS)
- Use secure networks when possible (avoid public WiFi)

## Update Notifications

Check with your team admin periodically for:
- App updates
- Server URL changes
- New features
- Bug fixes

---

**Version:** 1.0.0  
**Last Updated:** 2025
