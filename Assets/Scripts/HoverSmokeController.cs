using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoverSmokeController : MonoBehaviour {
    public bool hover = false;
    public AudioSource audioSource;
    public ParticleSystem hoverParticles;

	void Start () 
    {
        audioSource = GetComponent<AudioSource>();
        hoverParticles = GetComponent<ParticleSystem>();
    }
	
	// Update is called once per frame
	void Update () {
        if (GameLogic.instance.gameState != GameLogic.GameStates.gameplay)
        {
            return;
        }
        if (hover)
        {
            if (audioSource != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
            if (hoverParticles != null && !hoverParticles.isPlaying) hoverParticles.Play();
        }
        else
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            if (hoverParticles != null && hoverParticles.isPlaying) hoverParticles.Stop();
        }
	}
}
