import { initializeApp, getApps, applicationDefault } from "firebase-admin/app";
import { getAuth } from "firebase-admin/auth";
import { getDatabase, ServerValue } from "firebase-admin/database";

export const firebaseEnabled = Boolean(process.env.FIREBASE_PROJECT_ID);
if (firebaseEnabled && !getApps().length) {
  initializeApp({
    credential: applicationDefault(),
    projectId: process.env.FIREBASE_PROJECT_ID,
    ...(process.env.FIREBASE_DATABASE_URL ? { databaseURL: process.env.FIREBASE_DATABASE_URL } : {})
  });
}
export async function authenticate(token: unknown): Promise<string> {
  if (!firebaseEnabled) return `guest-${Math.random().toString(36).slice(2, 11)}`;
  if (typeof token !== "string" || !token) throw Error("Firebase ID token required");
  const decoded = await getAuth().verifyIdToken(token);
  return decoded.uid;
}
export async function roomRecord(id: string, data: Record<string, unknown> | null) {
  if (!firebaseEnabled || !process.env.FIREBASE_DATABASE_URL) return;
  try {
    await getDatabase().ref(`rooms/${id}`).set(data ? {...data, updatedAt: ServerValue.TIMESTAMP} : null);
  } catch(e) { console.warn("Firebase room metadata sync failed", e); }
}
