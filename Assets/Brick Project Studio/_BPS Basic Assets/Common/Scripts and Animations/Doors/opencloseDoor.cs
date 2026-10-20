using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SojaExiles
{
    public class opencloseDoor : MonoBehaviour
    {
        public Animator openandclose;
        public bool open;
        public Transform Player;

        [Header("Interaction")]
        public float interactDistance = 3f;   // Jarak maksimal bisa buka pintu
        public Key interactKey = Key.E;       // Tombol buka/tutup

        void Start()
        {
            open = false;
        }

        void Update()
        {
            if (Player == null) return;

            float dist = Vector3.Distance(Player.position, transform.position);

            // Kalau player dekat dan tekan E
            if (dist <= interactDistance && Keyboard.current[interactKey].wasPressedThisFrame)
            {
                if (!open)
                {
                    StartCoroutine(opening());
                }
                else
                {
                    StartCoroutine(closing());
                }
            }
        }

        IEnumerator opening()
        {
            openandclose.Play("Opening");
            open = true;
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator closing()
        {
            openandclose.Play("Closing");
            open = false;
            yield return new WaitForSeconds(0.5f);
        }
    }
}