# Life Manager V3

## New

### Voice shopping list
- `SpeechRecognition` / `webkitSpeechRecognition`, language `ru-RU`.
- User sees and can edit the transcript before saving.
- `POST /api/shopping/bulk` saves up to 50 distinct items in one operation.
- Lightweight server-side category inference for food, home, pet, health, education, fitness and appliances.
- Manual multi-item fallback remains usable when browser speech recognition is unavailable.

### Mood, interests and reading
- Daily mood check-in on Today (`great`, `good`, `neutral`, `tired`, `low`) with energy 1–5.
- Mood is persisted in the user's JSON data and affects advice.
- Profile has `Interests`; comma/semicolon/newline separated.
- Hobby suggestions take current mood/energy into account.
- `GET /api/reading-suggestions` queries Russian Wikipedia and returns up to three articles based on interests; horoscope theme supplies a fallback topic.

### Legal helper V3
- Broad area keyword matching replaced by scenario-level ranking.
- Exact phrases have high weight; word-root signals have lower weight.
- User-selected category is only a weak hint and cannot easily override the description.
- Competing scenario detection, classification confidence, matched signals and follow-up questions.
- Scenario-specific sources, including direct ConsultantPlus articles for common consumer and employment cases.
- The confidence shown is classification confidence, not a probability of winning a legal dispute.

### Image of the day
- Client-side Puter.js integration; no Life Manager API key is stored.
- Explicit user click starts generation.
- Prompt uses only day theme, city and selected mood; it does not send account email or shopping/task data.
- Generated image is cached only in browser `sessionStorage` when possible.
- Puter may request a one-time consent/auth flow and can create a temporary user session.

## Validation
- `node --check` passes for `app.js` and `sw.js`.
- Shell scripts pass `bash -n`.
- JSON/YAML files parse successfully.
- C# delimiter balance checked for all project source files.
- A .NET SDK is not present in the artifact environment; GitHub Actions remains the authoritative `dotnet restore/build/publish` compile check before deployment.
