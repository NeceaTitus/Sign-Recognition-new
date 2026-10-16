package com.unisign.speech;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import java.util.ArrayList;

/**
 * Wraps android.speech.SpeechRecognizer for continuous dictation (default ro-RO)
 * and forwards results back to Unity via UnitySendMessage.
 * Includes intelligent backoff for network/server errors and clean lifecycle handling.
 */
public class SpeechRecognizerPlugin implements RecognitionListener {

    private static final String TAG = "SpeechRecognizerPlugin";
    private static final long DEFAULT_RESTART_DELAY_MS = 400;
    private static final long MAX_BACKOFF_DELAY_MS = 10000;
    private static final long BASE_NETWORK_BACKOFF_MS = 1000;

    private final String unityGameObjectName;
    private final Activity activity;
    private final Handler mainHandler;

    private String language = "ro-RO";
    private SpeechRecognizer speechRecognizer;
    private Intent recognizerIntent;
    private boolean listeningRequested;
    private boolean destroyed;
    private int consecutiveErrorsCount = 0;

    public SpeechRecognizerPlugin(String unityGameObjectName) {
        this(unityGameObjectName, "ro-RO");
    }

    public SpeechRecognizerPlugin(String unityGameObjectName, String language) {
        this.unityGameObjectName = unityGameObjectName;
        if (language != null && !language.trim().isEmpty()) {
            this.language = language;
        }
        this.activity = UnityPlayer.currentActivity;
        this.mainHandler = new Handler(Looper.getMainLooper());

        mainHandler.post(this::initializeAndStart);
    }

    // ---- Public API, callable from C# via AndroidJavaObject.Call ----

    public void setLanguage(String newLanguage) {
        mainHandler.post(() -> {
            if (newLanguage != null && !newLanguage.trim().isEmpty()) {
                this.language = newLanguage;
                if (recognizerIntent != null) {
                    recognizerIntent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, this.language);
                }
            }
        });
    }

    public void startListening() {
        mainHandler.post(() -> {
            listeningRequested = true;
            consecutiveErrorsCount = 0;
            if (speechRecognizer == null) {
                initializeAndStart();
            } else {
                startListeningInternal();
            }
        });
    }

    public void stopListening() {
        mainHandler.post(() -> {
            listeningRequested = false;
            consecutiveErrorsCount = 0;
            if (speechRecognizer != null) {
                try {
                    speechRecognizer.stopListening();
                } catch (Exception e) {
                    Log.w(TAG, "stopListening exception: " + e.getMessage());
                }
            }
        });
    }

    public void destroy() {
        mainHandler.post(() -> {
            destroyed = true;
            listeningRequested = false;
            if (speechRecognizer != null) {
                try {
                    speechRecognizer.destroy();
                } catch (Exception e) {
                    Log.w(TAG, "destroy exception: " + e.getMessage());
                }
                speechRecognizer = null;
            }
        });
    }

    // ---- Internal setup ----

    private void initializeAndStart() {
        if (destroyed || activity == null) {
            return;
        }

        if (!SpeechRecognizer.isRecognitionAvailable(activity)) {
            Log.e(TAG, "SpeechRecognizer.isRecognitionAvailable() = false pe acest dispozitiv.");
            UnityPlayer.UnitySendMessage(unityGameObjectName, "OnSpeechError",
                    "ERROR_RECOGNIZER_UNAVAILABLE: no speech recognition service on this device");
            return;
        }

        speechRecognizer = SpeechRecognizer.createSpeechRecognizer(activity);
        speechRecognizer.setRecognitionListener(this);

        recognizerIntent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        recognizerIntent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        recognizerIntent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, language);
        recognizerIntent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true);
        recognizerIntent.putExtra(RecognizerIntent.EXTRA_CALLING_PACKAGE, activity.getPackageName());

        listeningRequested = true;
        startListeningInternal();
    }

    private void startListeningInternal() {
        if (destroyed || !listeningRequested || speechRecognizer == null || recognizerIntent == null) {
            return;
        }

        try {
            speechRecognizer.startListening(recognizerIntent);
        } catch (Exception e) {
            Log.e(TAG, "startListening() a esuat: " + e.getMessage());
            scheduleRestart(DEFAULT_RESTART_DELAY_MS);
        }
    }

    private void scheduleRestart(long delayMs) {
        if (destroyed || !listeningRequested) {
            return;
        }
        mainHandler.postDelayed(this::startListeningInternal, delayMs);
    }

    // ---- RecognitionListener callbacks (invoked on the main thread) ----

    @Override
    public void onReadyForSpeech(Bundle params) {}

    @Override
    public void onBeginningOfSpeech() {
        consecutiveErrorsCount = 0;
    }

    @Override
    public void onRmsChanged(float rmsdB) {}

    @Override
    public void onBufferReceived(byte[] buffer) {}

    @Override
    public void onEndOfSpeech() {}

    @Override
    public void onError(int error) {
        String description = describeError(error);
        Log.w(TAG, "onError: " + description);
        UnityPlayer.UnitySendMessage(unityGameObjectName, "OnSpeechError", error + ": " + description);

        if (error == SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS) {
            Log.e(TAG, "Permisiuni insuficiente pentru inregistrare audio. Oprire restart.");
            listeningRequested = false;
            return;
        }

        long delay = DEFAULT_RESTART_DELAY_MS;
        if (error == SpeechRecognizer.ERROR_NETWORK ||
            error == SpeechRecognizer.ERROR_NETWORK_TIMEOUT ||
            error == SpeechRecognizer.ERROR_SERVER) {
            consecutiveErrorsCount++;
            delay = Math.min(MAX_BACKOFF_DELAY_MS, BASE_NETWORK_BACKOFF_MS * (1L << Math.min(consecutiveErrorsCount, 4)));
            Log.w(TAG, "Eroare de retea/server. Backoff restart la " + delay + "ms (incercarea " + consecutiveErrorsCount + ")");
        } else if (error == SpeechRecognizer.ERROR_RECOGNIZER_BUSY) {
            delay = 1000;
        } else {
            consecutiveErrorsCount = 0;
        }

        scheduleRestart(delay);
    }

    @Override
    public void onResults(Bundle results) {
        consecutiveErrorsCount = 0;
        String text = firstResult(results);
        if (text != null && !text.isEmpty()) {
            UnityPlayer.UnitySendMessage(unityGameObjectName, "OnSpeechResult", text);
        }
        scheduleRestart(DEFAULT_RESTART_DELAY_MS);
    }

    @Override
    public void onPartialResults(Bundle partialResults) {
        consecutiveErrorsCount = 0;
        String text = firstResult(partialResults);
        if (text != null && !text.isEmpty()) {
            UnityPlayer.UnitySendMessage(unityGameObjectName, "OnSpeechPartialResult", text);
        }
    }

    @Override
    public void onEvent(int eventType, Bundle params) {}

    // ---- Helpers ----

    private String firstResult(Bundle bundle) {
        if (bundle == null) {
            return null;
        }
        ArrayList<String> matches = bundle.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
        if (matches == null || matches.isEmpty()) {
            return null;
        }
        return matches.get(0);
    }

    private String describeError(int error) {
        switch (error) {
            case SpeechRecognizer.ERROR_AUDIO: return "ERROR_AUDIO";
            case SpeechRecognizer.ERROR_CLIENT: return "ERROR_CLIENT";
            case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS: return "ERROR_INSUFFICIENT_PERMISSIONS";
            case SpeechRecognizer.ERROR_NETWORK: return "ERROR_NETWORK";
            case SpeechRecognizer.ERROR_NETWORK_TIMEOUT: return "ERROR_NETWORK_TIMEOUT";
            case SpeechRecognizer.ERROR_NO_MATCH: return "ERROR_NO_MATCH";
            case SpeechRecognizer.ERROR_RECOGNIZER_BUSY: return "ERROR_RECOGNIZER_BUSY";
            case SpeechRecognizer.ERROR_SERVER: return "ERROR_SERVER";
            case SpeechRecognizer.ERROR_SPEECH_TIMEOUT: return "ERROR_SPEECH_TIMEOUT";
            default: return "ERROR_UNKNOWN_" + error;
        }
    }
}
