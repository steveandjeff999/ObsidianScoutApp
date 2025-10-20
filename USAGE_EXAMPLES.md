# OBSIDIAN Scout App - Usage Examples

## Example Scenario 1: Local Development Setup

You're developing locally and running the API server on your development machine.

### Configuration
```
Protocol: http
Server Address: 192.168.1.100
Port: 8080
```

**Steps:**
1. Start your local API server
2. Find your computer's IP address (192.168.1.100)
3. Open the app on your test device (on same network)
4. Tap "?? Server Configuration"
5. Select "http" from Protocol picker
6. Enter "192.168.1.100" in Server Address
7. Enter "8080" in Port
8. Tap "Test Connection" to verify
9. Tap "Save"
10. Login with test credentials:
    - Username: test_scout
    - Password: TestPass123
    - **Team Number: 5454**

## Example Scenario 2: Production Server

Your team has deployed the API to a cloud server with a custom domain.

### Configuration
```
Protocol: https
Server Address: scout.team5454.com
Port: 443
```

**Steps:**
1. Open the app
2. Tap "?? Server Configuration"
3. Select "https" from Protocol picker
4. Enter "scout.team5454.com" in Server Address
5. Enter "443" in Port
6. Preview shows: `https://scout.team5454.com:443`
7. Tap "Test Connection"
8. See "? Connection successful"
9. Tap "Save"
10. Login with your credentials:
    - Username: scout_john
    - Password: SecurePass456
    - **Team Number: 5454**

## Example Scenario 3: Scouting at Competition

You're at a competition and need to scout multiple matches.

### Match 1: Qualification Match #12, Team 1234
```
1. Login to the app (username/password/team number)
2. Tap "?? Scout Match"
3. Enter Team ID: 1234
4. Enter Match ID: 12
5. During Autonomous:
   - Robot scores 2 speaker notes: Tap "+" twice on Auto Speaker
   - Robot scores 1 amp note: Tap "+" once on Auto Amp
6. During Teleop:
   - Robot scores 8 speaker notes: Tap "+" 8 times on Teleop Speaker
   - Robot scores 3 amp notes: Tap "+" 3 times on Teleop Amp
7. During Endgame:
   - Robot successfully climbs: Select "successful" from picker
8. Add notes: "Strong autonomous, consistent shooter, fast climber"
9. Tap "Submit Scouting Data"
10. See "? Scouting data submitted successfully!"
11. Tap "Reset Form" for next match
```

**Submitted Data:**
```json
{
  "team_id": 1234,
  "match_id": 12,
  "data": {
    "auto_speaker_scored": 2,
    "auto_amp_scored": 1,
    "teleop_speaker_scored": 8,
    "teleop_amp_scored": 3,
    "endgame_climb": "successful",
    "notes": "Strong autonomous, consistent shooter, fast climber"
  }
}
```

### Match 2: Qualification Match #13, Team 5678
```
1. Enter Team ID: 5678
2. Enter Match ID: 13
3. Autonomous:
   - 0 speaker notes (don't tap anything)
   - 0 amp notes
4. Teleop:
   - 5 speaker notes
   - 2 amp notes
5. Endgame:
   - Climb attempted but failed: Select "failed"
6. Notes: "Missed autonomous, defense bot, climb mechanism broke"
7. Submit
```

## Example Scenario 4: Team Research

You want to research teams before your next match.

### Viewing Team Information
```
1. Login to app
2. Tap "?? View Teams"
3. Scroll through team list
4. See teams with their:
   - Team number (e.g., 5454)
   - Team name (e.g., "The Bionics")
   - Location (e.g., "Kansas City, MO")
   - Scouting entries count (e.g., "15 entries")
5. Pull down to refresh list
```

### Viewing Event Schedule
```
1. From home screen, tap "?? View Events"
2. See your events:
   - Greater Kansas City Regional
   - Location: Kansas City, MO
   - Date: Mar 14-16, 2024
   - 45 teams attending
3. Pull down to refresh
```

## Example Scenario 5: Server Migration

Your team is moving from a local server to a cloud server mid-competition.

### Changing Server Configuration
```
Old Server:
- http://192.168.1.100:8080

New Server:
- https://scout.team5454.com:443

Steps:
1. Tap "?? Server Configuration" on login screen
2. Change Protocol to "https"
3. Change Address to "scout.team5454.com"
4. Change Port to "443"
5. Preview shows: https://scout.team5454.com:443
6. Tap "Test Connection"
7. Verify "? Connection successful"
8. Tap "Save"
9. Login with same credentials (token is invalidated on server change)
   - Username: scout_john
   - Password: SecurePass456
   - **Team Number: 5454**
```

## Example Scenario 6: Error Recovery

Something goes wrong and you need to troubleshoot.

### Connection Failed Error
```
Error: "? Connection failed: Connection refused"

Troubleshooting:
1. Check if you're on the correct WiFi network
2. Verify server is running
3. Tap "?? Server Configuration"
4. Verify server address is correct
5. Try "Test Connection"
6. If still failing, check with team admin
```

### Authentication Expired
```
Error: "? Invalid credentials"

Possible Causes:
- Token expired (after 7 days)
- Server was restarted
- Credentials changed
- Wrong team number entered

Solution:
1. Tap "Logout" if visible
2. Or close and reopen app
3. Login again with current credentials
4. **Make sure to enter the correct team number**
```

### Wrong Team Number Error
```
Error: "? Invalid credentials" or "? Login failed"

Possible Cause:
- Entered wrong team number

Solution:
1. Verify your team number with team admin
2. Common team numbers are 1-9999
3. Re-enter credentials with correct team number
4. Example: Team 5454 should enter "5454" not "05454" or "5454.0"
```

### Submission Failed
```
Error: "? Submission failed: Team not found"

Possible Causes:
- Wrong Team ID entered
- Team not registered for this event

Solution:
1. Double-check Team ID
2. Verify team is at your event
3. Check with scouting coordinator
4. Try again with correct Team ID
```

## Example Scenario 7: First Time User

A new scout is using the app for the first time.

### Complete First-Time Walkthrough
```
Day 1 - Setup:
1. Install app on phone
2. Open app
3. See login screen
4. Tap "?? Server Configuration"
5. Team admin provides:
   - Server: https://scout.team5454.com
   - Port: 443
   - Username: scout_john
   - Password: SecurePass123!
   - Team Number: 5454
6. Enter server details:
   - Protocol: https
   - Address: scout.team5454.com
   - Port: 443
7. Tap "Test Connection"
8. See "? Connection successful"
9. Tap "Save"
10. Enter username: scout_john
11. Enter password: SecurePass123!
12. **Enter team number: 5454**
13. Tap "Login"
14. See home screen
15. Explore the app:
    - Tap "?? View Teams" to see team list
    - Tap back
    - Tap "?? View Events" to see events
    - Tap back

Day 2 - First Match Scouting:
1. Open app (automatically logs in with saved token)
2. Tap "?? Scout Match"
3. Get match assignment: "Scout Team 1234 in Match 5"
4. Enter Team ID: 1234
5. Enter Match ID: 5
6. Watch the match and record data
7. Submit after match ends
8. Repeat for next assignment
```

## Example Scenario 8: Competition Day Workflow

Full day workflow for a dedicated scout.

### Morning Setup (Before Matches)
```
7:00 AM - Arrive at venue
1. Connect to venue WiFi
2. Open app
3. Verify automatic login
4. Tap "?? View Events" to confirm correct event
5. Pull down to refresh
6. Tap "?? View Teams" to see team list
7. Pull down to refresh
8. Ready to scout!
```

### During Matches
```
Match 1 (9:00 AM):
- Scout Team 1234
- Record all data
- Submit immediately after match

Match 2 (9:20 AM):
- Scout Team 5678
- Record all data
- Submit immediately

[Continue for 10-12 matches per day]
```

### End of Day
```
5:00 PM - Done Scouting:
1. Verify all data submitted (check with lead scout)
2. Leave app logged in for next day
3. Close app

Note: Token valid for 7 days, so no need to re-enter credentials daily
```

## Example Scenario 9: Multi-Team Scout

A scout working with multiple teams needs to switch between team contexts.

### Switching Teams
```
Scenario: You're helping Team 5454 and Team 1234

For Team 5454 scouting:
1. Logout from app
2. Login with Team 5454 credentials:
   - Username: scout_john_5454
   - Password: Pass5454
   - Team Number: 5454
3. Scout matches for Team 5454
4. Data is scoped to Team 5454

For Team 1234 scouting:
1. Logout from app
2. Login with Team 1234 credentials:
   - Username: scout_john_1234
   - Password: Pass1234
   - Team Number: 1234
3. Scout matches for Team 1234
4. Data is scoped to Team 1234

Note: Team number ensures data isolation between teams
```

## Common Patterns

### Quick Match Entry Pattern
```
1. Open app (already logged in)
2. Tap "?? Scout Match"
3. Enter IDs (Team & Match)
4. Use +/- buttons to count quickly
5. Select climb status
6. Add brief notes
7. Submit
8. Reset
9. Repeat
```

### Research Pattern
```
1. Between matches, browse teams
2. Pull to refresh for latest data
3. Check scouting entry counts
4. Identify high-performing teams
5. Share insights with drive team
```

### Maintenance Pattern
```
Once per event:
1. Verify server configuration
2. Test connection
3. Check for app updates
4. Confirm credentials work (username/password/team number)
5. Clear any test data
```

## Login Credential Examples

### Valid Login Formats
```
? Correct:
Username: scout_john
Password: SecurePass123!
Team Number: 5454

? Correct:
Username: jdoe
Password: MyP@ssw0rd
Team Number: 254

? Correct:
Username: scout1
Password: TempPass2024
Team Number: 1
```

### Invalid Login Formats
```
? Wrong - Missing team number:
Username: scout_john
Password: SecurePass123!
Team Number: [blank]

? Wrong - Invalid team number format:
Username: scout_john
Password: SecurePass123!
Team Number: Team5454  (should be just: 5454)

? Wrong - Non-numeric team number:
Username: scout_john
Password: SecurePass123!
Team Number: abc
```

---

**Pro Tips:**

?? **Keep the app open** between matches to avoid delays  
?? **Submit immediately** after each match  
?? **Use notes field** for important observations  
?? **Double-check IDs** before submitting  
?? **Pull to refresh** lists regularly for latest data  
?? **Test your setup** before competition starts  
?? **Keep your device charged** throughout the day  
?? **Remember your team number** - you need it for every login  
?? **Team number must be numeric** - don't include text like "Team" or "FRC"
