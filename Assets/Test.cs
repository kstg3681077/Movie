using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public struct MovieInfo
{
    public byte[] m_pData;
    public int m_nWidth;
    public int m_nHeight;
    public float m_fPts;

    public MovieInfo(byte[] p, int width, int height, float time)
    {
        m_pData = p;
        m_nHeight = height;
        m_nWidth = width;
        m_fPts = time;
    }
};

public struct SubtitleInfo
{
    public string m_pSubtitle;
    public float m_fStartTime;
    public float m_fEndTime;

    public SubtitleInfo(string str, float start, float end)
    {
        m_pSubtitle = str;
        m_fStartTime = start;
        m_fEndTime = end;
    }
};

public class Test : MonoBehaviour
{
    [DllImport("Dll1", EntryPoint = "Open", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int Open([MarshalAs(UnmanagedType.LPStr)]string url, int transport_type, [MarshalAs(UnmanagedType.LPStr)] string udp_buffer_size, [MarshalAs(UnmanagedType.LPStr)] string time_out, long time_sec, bool is_stream);

    [DllImport("Dll1", EntryPoint = "GetVideoBuffer")]
    public static extern IntPtr GetVideoBuffer(ref int length, ref int width, ref int height, ref double time);

    [DllImport("Dll1", EntryPoint = "Close")]
    public static extern void Close();

    [DllImport("Dll1", EntryPoint = "Init")]
    public static extern bool Init(int buffer_count);

    [DllImport("Dll1", EntryPoint = "GetVideoLength")]
    public static extern double GetVideoLength(ref int hour, ref int min, ref int sec);

    [DllImport("Dll1", EntryPoint = "FreeVideoBuffer")]
    public static extern bool FreeVideoBuffer();

    [DllImport("Dll1", EntryPoint = "GetAudioBuffer")]
    public static extern IntPtr GetAudioBuffer(ref int len, ref int channels, ref int sample_rate, ref double time);

    [DllImport("Dll1", EntryPoint = "FreeAudioBuffer")]
    public static extern bool FreeAudioBuffer();

    [DllImport("Dll1", EntryPoint = "GetSubtitle")]
    public static extern IntPtr GetSubtitle(ref int len, ref double start_time, ref double end_time);

    [DllImport("Dll1", EntryPoint = "FreeSubtitle")]
    public static extern bool FreeSubtitle();

    Queue<MovieInfo> m_pMovie;

    Queue<SubtitleInfo> m_pSubtitle;

    Text m_pSubtitleText;

    AudioClip m_pClip;

    AudioSource m_pAudio;

    RawImage m_pImage;

    Thread m_pThread;

    public InputField f;

    float m_fStartOffset = 0f;
    int m_nAudioDataOffset = 0;

    long m_nSeekTimeSeconds = 0;

    int m_nVideoLength_Sec = 0;

    private void Awake()
    {
        m_pSubtitleText = transform.parent.Find("Subtitle").GetComponent<Text>();

        PointHandle pHandle = transform.parent.GetComponentInChildren<PointHandle>();
        pHandle.m_pSetSeek = Seek;
        pHandle.m_pGetCurrentTime = () =>
        {
            if (m_nVideoLength_Sec == 0)
            {
                return 0;
            }
            else
            {
                float fValue = (m_fStartOffset + m_pAudio.time) / m_nVideoLength_Sec;

                return fValue;
            }
        };
    }

    void Start()
    {
        string name = Dns.GetHostName();
        IPAddress[] ipadrlist = Dns.GetHostAddresses(name);
        foreach (IPAddress ipa in ipadrlist)
        {
            if (ipa.AddressFamily == AddressFamily.InterNetwork)
                Debug.Log(ipa.ToString());
        }

        f.onEndEdit.AddListener((str) => { url = str; Begin(m_nSeekTimeSeconds); });
    }

    void Update()
    {
        if (m_pMovie != null)
        {
            GetVideoSec();

            UpdateVideoData();

            UpdateAudioData();

            UpdateSubtitleData();

            AudioClipReScale();

            SubtitleScale();
        }
    }

    void Begin(long time_sec)
    {
        m_fStartOffset = 0f;
        m_nAudioDataOffset = 0;

        m_pAudio = GetComponent<AudioSource>();

        if (m_pAudio == null)
        {
            m_pAudio = gameObject.AddComponent<AudioSource>();
        }

        m_pAudio.playOnAwake = false;
        m_pAudio.loop = true;

        m_pImage = GetComponent<RawImage>();

        if (m_pImage.texture != null)
        {
            Destroy(m_pImage.texture);
        }

        m_pMovie = new Queue<MovieInfo>(0);
        m_pSubtitle = new Queue<SubtitleInfo>(0);

        m_pThread = new Thread(Open1);

        m_pThread.Start(time_sec);

        StartCoroutine(FrameOffset());

        StartCoroutine(StartDelay(20));
    }

    string url;

    bool b = false;

    void Open1(object time_sec)
    {
        long nSeekTime = (long)time_sec;

        m_nVideoLength_Sec = 0;

        if (Init(30))
        {
            Debug.Log("Start");
        }

        Debug.Log(nSeekTime);
        Debug.Log(url);
        //int ret = Open("rtsp://192.168.3.125:8554/11", 0, "65536000", "3000000", nSeekTime,false);
        int ret = Open(url, 0, "1024000", "3000000", nSeekTime, false);
        //int ret = Open("F:/KN1.mkv", 0, "1024000", "3000000", nSeekTime, false);
        throw new Exception(ret.ToString());
    }

    void UpdateVideoData()
    {
        int nVideoLen = 0;
        int nWidth = 0;
        int nHeight = 0;
        double dVideoTime = 0;

        while (m_pMovie.Count < 30)
        {
            IntPtr p = GetVideoBuffer(ref nVideoLen, ref nWidth, ref nHeight, ref dVideoTime);

            if (p == IntPtr.Zero)
            {
                break;
            }

            Debug.Log(dVideoTime);

            byte[] pBytes = new byte[nVideoLen];

            Marshal.Copy(p, pBytes, 0, pBytes.Length);

            m_pMovie.Enqueue(new MovieInfo(pBytes, nWidth, nHeight, (float)dVideoTime));

            FreeVideoBuffer();
        }
    }

    void UpdateAudioData()
    {
        int nAudioLen = 0;
        int nChannels = 0;
        int nSampleRate = 0;
        double dAudioTime = 0;

        while (true)
        {
            IntPtr p = GetAudioBuffer(ref nAudioLen, ref nChannels, ref nSampleRate, ref dAudioTime);

            if (p == IntPtr.Zero)
            {
                break;
            }

            if (m_pAudio.clip == null)
            {
                m_pClip = AudioClip.Create("New", 600 * nSampleRate, nChannels, nSampleRate, false);
                m_pAudio.clip = m_pClip;

                m_fStartOffset = (float)dAudioTime;
                Debug.Log("::::" + dAudioTime);
            }

            float[] pData = new float[nAudioLen / 4];

            Marshal.Copy(p, pData, 0, pData.Length);

            m_pClip.SetData(pData, m_nAudioDataOffset);

            m_nAudioDataOffset += pData.Length / nChannels;

            if (m_nAudioDataOffset >= 600 * nSampleRate)
            {
                Debug.LogWarning("ChangeOffset");
                m_nAudioDataOffset -= 600 * nSampleRate;
            }

            FreeAudioBuffer();

            //Debug.Log(dAudioTime);
        }
    }

    void UpdateSubtitleData()
    {
        int nLen = 0;
        double dStart = 0;
        double dEnd = 0;

        while (true)
        {
            IntPtr p = GetSubtitle(ref nLen, ref dStart, ref dEnd);

            if (p == IntPtr.Zero)
            {
                break;
            }

            string s = Marshal.PtrToStringUni(p);

            FreeSubtitle();

            string[] strs = s.Split(new char[2] { ',', ',' });

            m_pSubtitle.Enqueue(new SubtitleInfo(strs[strs.Length - 1], (float)dStart, (float)dEnd));
        }
    }

    private void OnDisable()
    {
        StopCoroutine(FrameOffset());

        StopCoroutine(StartDelay(20));

        Close();

        m_pMovie = null;
        m_pSubtitle = null;
        GC.Collect();

        if (m_pAudio.isPlaying)
        {
            m_pAudio.Stop();
        }

        if (m_pClip != null)
        {
            m_pAudio.clip = null;
            Destroy(m_pClip);
        }

        m_pThread.Abort();
    }

    public void A()
    {
        gameObject.SetActive(false);
    }

    public void B()
    {
        gameObject.SetActive(true);
    }

    public void Seek(float value)
    {
        long nSeekTime = (long)(value * m_nVideoLength_Sec);

        gameObject.SetActive(false);
        m_nSeekTimeSeconds = nSeekTime;
        gameObject.SetActive(true);
        Begin(m_nSeekTimeSeconds);
    }

    IEnumerator FrameOffset()
    {
        while (true)
        {
            float fCurrentPts = -1f;

            if (m_pAudio.isPlaying)
            {
                float fTime = m_pAudio.time + m_fStartOffset;

                while (m_pMovie.Count > 0)
                {
                    MovieInfo pCurrent = m_pMovie.Dequeue();

                    if (pCurrent.m_fPts - fTime > 0.2f)
                    {
                        m_pAudio.time = pCurrent.m_fPts - m_fStartOffset;
                        //Debug.LogWarning("Time Offset" + pCurrent.m_fPts + " " + fTime);
                    }
                    else if (pCurrent.m_fPts - fTime < -0.2f)
                    {
                        Debug.LogWarning("Useless Frame");
                        //Debug.Log("FrameTime:" + pCurrent.m_fPts);
                        //Debug.Log("AudioTime:" + (m_pAudio.time + m_fStartOffset));
                        continue;
                    }

                    if (m_pImage.texture != null)
                    {
                        Destroy(m_pImage.texture);
                    }

                    Texture2D pTex = new Texture2D(pCurrent.m_nWidth, pCurrent.m_nHeight, TextureFormat.RGB24, false);
                    pTex.LoadRawTextureData(pCurrent.m_pData);
                    pTex.Apply();

                    fCurrentPts = pCurrent.m_fPts;
                    m_pImage.texture = pTex;
                    break;
                }
            }



            if (fCurrentPts == -1 || m_pMovie.Count <= 0)
            {
                yield return null;
            }
            else
            {
                float fNextPts = m_pMovie.Peek().m_fPts;

                float fDValue = fNextPts - fCurrentPts;

                if (fDValue <= 0)
                {
                    Debug.LogWarning("!!!!!!!!!!!!!!!!!!!!!!" + "::::::::" + fDValue);
                    yield return null;
                }
                else
                {
                    //Debug.Log(fDValue * 0.95f);
                    yield return new WaitForSeconds(fDValue * 0.95f);
                }
            }
        }
    }

    IEnumerator StartDelay(int FrameCout)
    {
        if (FrameCout <= 0)
        {
            Debug.LogError("FrameCout <= 0!!!!!!!");
            yield break;
        }

        while (true)
        {
            if (m_pAudio.clip != null && !m_pAudio.isPlaying && m_pMovie.Count > FrameCout)
            {
                float fStart = m_pMovie.Peek().m_fPts;

                if (fStart < m_fStartOffset)
                {
                    int nNum = 0;
                    foreach (MovieInfo item in m_pMovie)
                    {
                        if (item.m_fPts > m_fStartOffset)
                        {
                            ++nNum;
                        }
                    }

                    if (nNum > FrameCout / 2)
                    {
                        m_pAudio.Play();
                        Debug.Log("1" + " " + m_fStartOffset);
                        yield break;
                    }
                }
                else
                {
                    m_pAudio.Play();
                    m_pAudio.time = fStart - m_fStartOffset;
                    Debug.Log("2" + " " + fStart + " " + m_fStartOffset);
                    yield break;
                }
            }

            yield return null;
        }
    }

    void AudioClipReScale()
    {
        if (m_pClip != null && m_pClip.length - m_pAudio.time < 0.033f)
        {
            Debug.LogWarning("OffsetChange");
            m_fStartOffset += 600f;
            m_pAudio.time = 0f;
        }
    }

    void SubtitleScale()
    {
        if (m_pSubtitle.Count <= 0)
        {
            return;
        }

        float fTime = m_fStartOffset + m_pAudio.time;

        SubtitleInfo pInfo = m_pSubtitle.Peek();

        float fStartOffset = fTime - pInfo.m_fStartTime;
        float fEndOffset = fTime - pInfo.m_fEndTime;

        if (fStartOffset >= 0.2f && fEndOffset <= 0.75f)
        {
            m_pSubtitleText.text = pInfo.m_pSubtitle;
        }
        else if (fEndOffset > 0.75f)
        {
            m_pSubtitleText.text = null;
            m_pSubtitle.Dequeue();
        }
        else
        {
            m_pSubtitleText.text = null;
        }
    }

    void GetVideoSec()
    {
        if (m_nVideoLength_Sec == 0)
        {
            int nHours = 0;
            int nMins = 0;
            int nSecs = 0;
            GetVideoLength(ref nHours, ref nMins, ref nSecs);

            m_nVideoLength_Sec = nHours * 3600 + nMins * 60 + nSecs;
        }
    }
}
