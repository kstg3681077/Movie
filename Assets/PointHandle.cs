using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class PointHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Action<float> m_pSetSeek;

    public Func<float> m_pGetCurrentTime;

    Slider m_pSlider;

    bool m_bIsSlidering = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.LogWarning("Slider");
        m_bIsSlidering = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.LogWarning("SliderEnd");

        if (m_pSetSeek != null)
        {
            m_pSetSeek(m_pSlider.value);
        }

        m_bIsSlidering = false;
    }

    void Awake()
    {
        m_pSlider = GetComponent<Slider>();
    }

    void Update()
    {
        if (m_pGetCurrentTime != null && !m_bIsSlidering)
        {
            float fValue = m_pGetCurrentTime();

            if (fValue != 0)
            {
                m_pSlider.value = fValue;
            }
        }
    }
}
