using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace taskAssets.Instructions
{
    /// <summary>
    /// Handles the audio for sequences of instructions that do not have an associated video they are edited into.
    /// Functions as 
    /// </summary>
    public class InstructionAudio : MonoBehaviour
    {

            public AudioSource audioSource;
            public bool isFinished = false;

            public void PlayAudioSequentially(IEnumerable<AudioClip> clips)
            {
                isFinished = false;
                StartCoroutine(PlaySequentially(clips));
            }

            public bool IsPlaying()
            {
                return audioSource.isPlaying;
            }

            public bool IsFinished()
            {
                return isFinished;
            }

            private IEnumerator PlaySequentially(IEnumerable<AudioClip> clips)
            {
                foreach (var clip in clips)
                {
                    if(isFinished)
                    {
                        yield break;
                    }
                    audioSource.clip = clip;
                    audioSource.Play();
                    while (audioSource.isPlaying)
                    {
                        if(Keyboard.current.spaceKey.wasPressedThisFrame)
                        {
                            audioSource.Stop();
                            isFinished = true;
                            yield break;
                        }
                        yield return null;
                    }
                }
                isFinished = true;
            }
            
            public void PlayAudio(AudioClip clip)
            {
                audioSource.clip = clip;
                audioSource.Play();
            }
        }
    }