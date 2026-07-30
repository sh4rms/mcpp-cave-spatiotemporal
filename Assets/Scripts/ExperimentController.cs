using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// NSF Experiment Controller
/// Experiment includes trials (animation + audio).
///
/// Elements in Unity:
///  1. "ExperimentController" Game Object (has this script attached)
///  2. Four face GameObjects (LeftHead, FrontLeftHead, FrontRightHead, RightHead)
///  3. Assign an AudioSource to each head GO and optionally to audioSourceOrigin.
///  4. All CSV files and media live under Assets/Stimuli/
/// </summary>
public class NSFExperimentController : MonoBehaviour
{
    private const bool USE_RESOURCES = false;

    [Header("Face GameObjects")]
    public GameObject leftHeadObject;
    public GameObject frontLeftHeadObject;
    public GameObject frontRightHeadObject;
    public GameObject rightHeadObject;

    [Header("Audio Sources (assign one per head, plus an optional origin source)")]
    public AudioSource audioSourceL;        // Left head
    public AudioSource audioSourceFL;       // Front-left head
    public AudioSource audioSourceFR;       // Front-right head
    public AudioSource audioSourceR;        // Right head
    public AudioSource audioSourceOrigin;   // Room origin (optional, for non-spatial trials)

    [Header("Paths (relative to Application.dataPath or Assets/)")]
    public string stimuliPath = "Stimuli";

    [Header("CSV file names")]
    public string temporalOffsetCSV = "nsf1-temporal-offset.csv";
    public string specsCSV          = "nsf1-specs.csv";
    public string keyCSV            = "nsf1-key.csv";

    [Tooltip("Which trial order file to use (1–4), selecting nsf1-X.csv from the Stimuli folder.")]
    public int groupNumber = 1;

    [Header("Timing")]
    public float ITI           = 5f;    // Seconds of black before each trial
    public float trialDuration = 15f;   // Seconds each trial runs

    // Temporal offsets keyed by filename (ex. "kylie-but.anim")
    private Dictionary<string, float> temporalOffsets = new Dictionary<string, float>();

    // Spec Rows
    private class SpecRow
    {
        public string utterance, syncAsync, actress, target;
        public float[] offsets = new float[4];   // 1-offset to 4-offset (ms)
    }
    private List<SpecRow> specs = new List<SpecRow>();

    // Key Rows
    private class KeyRow
    {
        public int    trial;
        public string utterance, actress, syncAsync, target;
        public string audioSource;   // Optional: 1=L, 2=FL, 3=FR, 4=R, 5=Origin. Defaults to target if omitted.
    }
    private List<KeyRow> keys = new List<KeyRow>();

    // Trial Order
    private List<int> trialOrder = new List<int>();

    void Start()
    {
        LoadAllCSVs();
        StartCoroutine(RunExperiment());
    }

    string StimuliDir => Path.Combine(Application.dataPath, stimuliPath);

    void LoadAllCSVs()
    {
        ParseTemporalOffsets(Path.Combine(StimuliDir, temporalOffsetCSV));
        ParseSpecs(Path.Combine(StimuliDir, specsCSV));
        ParseKey(Path.Combine(StimuliDir, keyCSV));
        // Load the selected group's trial order file
        string trialOrderFile = $"nsf1-{groupNumber}.csv";
        trialOrder = ParseTrialOrder(Path.Combine(StimuliDir, trialOrderFile));
    }

    // nsf1-temporal-offset.csv
    // Title Row: filename, temp-offset (ms)
    // Example: kylie-but.anim, 120
    void ParseTemporalOffsets(string path)
    {
        if (!File.Exists(path)) { Debug.LogWarning($"[NSF] Not found: {path}"); return; }
        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)   // Skip first row
        {
            string[] cols = SplitCSV(lines[i]);
            if (cols.Length < 2) continue;
            if (float.TryParse(cols[1].Trim(), out float ms))
                temporalOffsets[cols[0].Trim()] = ms;
        }
        Debug.Log($"[NSF] Loaded {temporalOffsets.Count} temporal offsets.");
    }

    // nsf1-specs.csv
    // Title Row: utterance, sync-async, actress, target, 1-offset, 2-offset, 3-offset, 4-offset
    void ParseSpecs(string path)
    {
        if (!File.Exists(path)) { Debug.LogWarning($"[NSF] Not found: {path}"); return; }
        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = SplitCSV(lines[i]);
            if (cols.Length < 8) continue;
            var row = new SpecRow {
                utterance = cols[0].Trim(), syncAsync = cols[1].Trim(),
                actress   = cols[2].Trim(), target    = cols[3].Trim()
            };
            for (int o = 0; o < 4; o++) float.TryParse(cols[4 + o].Trim(), out row.offsets[o]);
            specs.Add(row);
        }
        Debug.Log($"[NSF] Loaded {specs.Count} spec rows.");
    }

    // nsf1-key.csv
    // Title Row: trial, utterance, actress, sync-async, target, audio-source (optional)
    // audio-source: 1=L, 2=FL, 3=FR, 4=R, 5=Origin. Defaults to target value if column is absent.
    void ParseKey(string path)
    {
        if (!File.Exists(path)) { Debug.LogWarning($"[NSF] Not found: {path}"); return; }
        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = SplitCSV(lines[i]);
            if (cols.Length < 5) continue;
            var row = new KeyRow {
                utterance   = cols[1].Trim(), actress   = cols[2].Trim(),
                syncAsync   = cols[3].Trim(), target    = cols[4].Trim(),
                audioSource = cols.Length > 5 && cols[5].Trim() != "" ? cols[5].Trim() : cols[4].Trim()
            };
            int.TryParse(cols[0].Trim(), out row.trial);
            keys.Add(row);
        }
        Debug.Log($"[NSF] Loaded {keys.Count} key rows.");
    }

    // nsf1-X.csv: Single column of trial numbers with header
    List<int> ParseTrialOrder(string path)
    {
        var list = new List<int>();
        if (!File.Exists(path)) { Debug.LogWarning($"[NSF] Not found: {path}"); return list; }
        foreach (string line in File.ReadAllLines(path))
            if (int.TryParse(line.Trim(), out int n)) list.Add(n);
        Debug.Log($"[NSF] Trial order from {Path.GetFileName(path)}: {list.Count} trials.");
        return list;
    }

    // CSV splitter helper function
    static string[] SplitCSV(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current   = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if      (c == '"')              inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else                             current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    IEnumerator RunExperiment()
    {
        foreach (int trialNum in trialOrder)
        {
            // Black offset before every trial
            yield return new WaitForSeconds(ITI);
            yield return StartCoroutine(RunTrial(trialNum));
        }

        Debug.Log("[NSF] Experiment complete.");
    }

    IEnumerator RunTrial(int trialNum)
    {
        Debug.Log($"[NSF] ─── Trial {trialNum} ───");

        // Find key row
        KeyRow key = keys.Find(k => k.trial == trialNum);
        if (key == null) { Debug.LogWarning($"[NSF] No key row for trial {trialNum}; skipping."); yield break; }

        // Match key to spec
        SpecRow spec = specs.Find(s =>
            s.utterance == key.utterance &&
            s.actress   == key.actress   &&
            s.syncAsync == key.syncAsync &&
            s.target    == key.target);
        if (spec == null) { Debug.LogWarning($"[NSF] No spec matched for trial {trialNum}; skipping."); yield break; }

        // Resolve clip name from actress + utterance  (ex. "kylie-but")
        string animBaseName  = $"{spec.actress}-{spec.utterance}";
        string animFileName  = $"{animBaseName}.anim";
        float  animTempOffMs = GetTemporalOffset(animFileName);

        AnimationClip clip = LoadAnimClip(animBaseName, animFileName);
        if (clip == null)
            Debug.LogWarning($"[NSF] Clip '{animFileName}' not found – animation skipped.");

        // Play all four heads immediately, seeking into the clip by the combined offset.
        // seekTime (s) = (per-head jitter ms + clip temporal offset ms) / 1000
        // This clips forward into the animation rather than adding a pre-trial delay.
        GameObject[] heads = { leftHeadObject, frontLeftHeadObject,
                                frontRightHeadObject, rightHeadObject };
        for (int i = 0; i < 4; i++)
        {
            if (heads[i] == null || clip == null) continue;
            float seekTime = (spec.offsets[i] + animTempOffMs) / 1000f;
            StartCoroutine(PlayClipOnHead(heads[i], clip, animBaseName, seekTime));
        }

        // Audio plays from the source specified in the key's audio-source column
        // (1=L, 2=FL, 3=FR, 4=R, 5=Origin; defaults to target position if unspecified)
        string audioFileName  = $"{animBaseName}.wav";
        float  audioTempOffMs = GetTemporalOffset(audioFileName);
        float  audioSeekTime  = audioTempOffMs / 1000f;
        AudioSource targetSource = GetAudioSourceForTarget(key.audioSource);
        StartCoroutine(PlayAudioFromSource(targetSource, animBaseName, audioFileName, audioSeekTime));

        yield return new WaitForSeconds(trialDuration);

        // Stop animations and audio
        foreach (var head in heads)
        {
            if (head == null) continue;
            Animation anim = head.GetComponent<Animation>();
            if (anim != null) anim.Stop();
        }
        // Stop whichever audio source is playing
        foreach (var src in new[] { audioSourceL, audioSourceFL, audioSourceFR, audioSourceR, audioSourceOrigin })
            if (src != null && src.isPlaying) src.Stop();
    }

    // Animation Helpers

    // Loads AnimationClip from Assets/Stimuli/ via AssetDatabase (editor) or Resources (build)
    AnimationClip LoadAnimClip(string baseName, string fileName)
    {
#if UNITY_EDITOR
        string assetPath = $"Assets/{stimuliPath}/{fileName}";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if (clip == null)
            Debug.LogWarning($"[NSF] Clip not found at '{assetPath}'.");
        else
            clip.legacy = true;   // Required for use with the legacy Animation component
        return clip;
#else
        string resPath = $"{stimuliPath}/{baseName}";
        AnimationClip clip = Resources.Load<AnimationClip>(resPath);
        if (clip == null)
            Debug.LogWarning($"[NSF] Clip not found at Resources/'{resPath}'.");
        return clip;
#endif
    }

    // Adds a legacy Animation component at runtime if missing, then plays the clip
    // seekSeconds advances into the clip so playback starts at the correct frame immediately
    IEnumerator PlayClipOnHead(GameObject head, AnimationClip clip,
                               string clipKey, float seekSeconds)
    {
        Animation anim = head.GetComponent<Animation>();
        if (anim == null)
            anim = head.AddComponent<Animation>();

        if (anim.GetClip(clipKey) == null)
            anim.AddClip(clip, clipKey);

        anim[clipKey].time = seekSeconds;
        anim.Play(clipKey);

        Debug.Log($"[NSF] {head.name}: playing '{clipKey}' (seek {seekSeconds:F3}s)");
        yield break;
    }

    // Audio Helpers

    // Returns the AudioSource for the given key value (1=L, 2=FL, 3=FR, 4=R, 5=Origin)
    // Defaults to audioSourceL if unrecognised
    AudioSource GetAudioSourceForTarget(string source)
    {
        switch (source.Trim())
        {
            case "1": return audioSourceL;
            case "2": return audioSourceFL;
            case "3": return audioSourceFR;
            case "4": return audioSourceR;
            case "5": return audioSourceOrigin;
            default:
                Debug.LogWarning($"[NSF] Unrecognised audio-source value '{source}'; defaulting to L.");
                return audioSourceL;
        }
    }

    // Loads audio and seeks forward by seekSeconds so playback starts at the correct sample
    IEnumerator PlayAudioFromSource(AudioSource source, string baseName, string fileName, float seekSeconds)
    {
        if (source == null) { Debug.LogWarning($"[NSF] No AudioSource for '{fileName}'; skipping audio."); yield break; }

        AudioClip clip = null;

#if UNITY_EDITOR
        string assetPath = $"Assets/{stimuliPath}/{fileName}";   // Must be .wav or .mp3, not .m4a
        clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        if (clip == null)
            Debug.LogWarning($"[NSF] AudioClip not found at '{assetPath}'.");
#else
        string resPath = $"{stimuliPath}/{baseName}";
        clip = Resources.Load<AudioClip>(resPath);
        if (clip == null)
            Debug.LogWarning($"[NSF] AudioClip not found at Resources/'{resPath}'.");
#endif

        if (clip == null) yield break;

        source.clip = clip;
        source.time = seekSeconds;
        source.Play();
        Debug.Log($"[NSF] Audio playing: '{fileName}' from source '{source.gameObject.name}' (seek {seekSeconds:F3}s)");
        yield break;
    }

    // Utility functions

    /// <summary>Returns the temporal offset (ms) for a given filename, or 0 if not found.</summary>
    float GetTemporalOffset(string filename) =>
        temporalOffsets.TryGetValue(filename, out float ms) ? ms : 0f;
}