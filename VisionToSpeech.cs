//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
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;




public class VisionToSpeech : MonoBehaviour
{
    public KeyCode activationKey = KeyCode.Keypad1;

    private string openAIApiKey = "API-key here :)";

    public Camera playerCamera; // The main camera for the player
    public Camera npcCamera; // The camera attached to the NPC

    public MainThreadDispatcher2 mainThreadDispatcher;

    [Serializable]
    public class TTSPayload
    {
        public string model;
        public string voice;
        public string input;
        public string response_format; // Add this line
    }


    void Start()
    {
        // Assign the main player camera if not already assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Ensure NPC camera is not enabled at the start
        if (npcCamera != null)
        {
            npcCamera.enabled = false;
        }
        else
        {
            Debug.LogError("NPC Camera is not assigned in the Inspector");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(activationKey))
        {
            StartCoroutine(CaptureImage());
        }
    }

    private IEnumerator CaptureImage()
    {
        if (npcCamera != null)
        {
            // Enable the NPC camera just for the capture
            npcCamera.enabled = true;

            // Wait for the end of the frame to ensure the camera has finished rendering
            yield return new WaitForEndOfFrame();

            // Ensure the NPC camera has a RenderTexture assigned
            if (npcCamera.targetTexture != null)
            {
                RenderTexture renderTexture = npcCamera.targetTexture;
                Texture2D renderResult = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
                RenderTexture.active = renderTexture;
                renderResult.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                renderResult.Apply();

                // Convert the image to a byte array, then to a base64 string
                byte[] imageBytes = renderResult.EncodeToPNG();
                Destroy(renderResult);
                string base64Image = Convert.ToBase64String(imageBytes);

                // Now you can send the base64Image to OpenAI
                StartCoroutine(SendImageToOpenAI(base64Image));
            }
            else
            {
                Debug.LogError("NPC Camera does not have a RenderTexture assigned.");
            }

            // Disable the NPC camera now that we're done with the capture
            npcCamera.enabled = false;
        }
        else
        {
            Debug.LogError("NPC Camera is not assigned.");
        }
    }

    
    private IEnumerator SendImageToOpenAI(string base64Image)
    {
        string openAIEndpoint = "https://api.openai.com/v1/chat/completions";
        string jsonPayload = "{\"model\":\"gpt-4-vision-preview\",\"messages\":[{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"What�s in this image?\"},{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/jpeg;base64," + base64Image + "\"}}]}],\"max_tokens\":500}";

        UnityWebRequest webRequest = UnityWebRequest.PostWwwForm(openAIEndpoint, "");
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonPayload);
        webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);

        yield return webRequest.SendWebRequest();

      

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError($"Error: {webRequest.error}");
        }
        else
        {
            string responseText = webRequest.downloadHandler.text;
            ProcessOpenAIResponse(responseText);
        }
    }

    private void ProcessOpenAIResponse(string responseJson)
    {
        // Use the static method Enqueue from MainThreadDispatcher2 directly
        MainThreadDispatcher2.Enqueue(() =>
        {
            OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(responseJson);
            if (response != null && response.choices != null && response.choices.Length > 0)
            {
                string description = response.choices[0].message.content;
                SpeakDescription(description);
            }
            else
            {
                UnityEngine.Debug.LogError("Failed to parse the OpenAI response or the response structure is not as expected: " + responseJson);
            }
        });
    }

    private void SpeakDescription(string text)
    {
        StartCoroutine(ConvertTextToSpeech(text));
    }

    private IEnumerator ConvertTextToSpeech(string text)
    {
        string openAITTSApiEndpoint = "https://api.openai.com/v1/audio/speech";

        TTSPayload payload = new TTSPayload
        {
            model = "tts-1",
            voice = "nova",
            input = text,
            response_format = "mp3" // Added to request MP3 format
        };
        string jsonPayload = JsonUtility.ToJson(payload);
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonPayload);

        using (UnityWebRequest ttsRequest = new UnityWebRequest(openAITTSApiEndpoint, "POST"))
        {
            ttsRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
            ttsRequest.downloadHandler = new DownloadHandlerBuffer();
            ttsRequest.SetRequestHeader("Content-Type", "application/json");
            ttsRequest.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);

            yield return ttsRequest.SendWebRequest();

            if (ttsRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error with TTS request: {ttsRequest.error}");
            }
            else
            {
                byte[] audioData = ttsRequest.downloadHandler.data;
                StartCoroutine(PlayAudioFromBytes(audioData));
            }
        }
    }

    // This method needs to be created to convert the byte array into an AudioClip and play it.
    private IEnumerator PlayAudioFromBytes(byte[] audioData)
    {
        // The audio data is now in MP3 format
        string tempAudioFileName = "temp_audio_clip.mp3";
        string filepath = Path.Combine(Application.persistentDataPath, tempAudioFileName);
        File.WriteAllBytes(filepath, audioData);

        // Load the audio file into an AudioClip using UnityWebRequest
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filepath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load AudioClip: {www.error}");
            }
            else
            {
                // Get the loaded AudioClip and play it
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                AudioSource audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = audioClip;
                audioSource.Play();
            }
        }

        // Optionally, delete the file after playback is done
        // For now, we'll delete it immediately (for simplicity)
        File.Delete(filepath);
    }



    [Serializable]
    private class OpenAIResponse
    {
        public Choice[] choices;
    }

    [Serializable]
    private class Choice
    {
        public Message message;
    }

    [Serializable]
    private class Message
    {
        public string content;
    }
}
