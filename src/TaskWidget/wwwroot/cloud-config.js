// Supabase project used for sync between the PC widget and the Android app.
// Supabase → Project Settings → API: "Project URL" and the "anon public" (or "publishable") key.
// The key is meant to be public: access to tasks is limited to the signed-in user by the
// row-level security rules in supabase/schema.sql.
window.TASKS_CLOUD = {
  url: "https://qmleeqatnekzgghnzuun.supabase.co",
  anonKey: "sb_publishable_Ww5WSYRPSK3p0fvjy87pow_vY5x_0ly",
};
