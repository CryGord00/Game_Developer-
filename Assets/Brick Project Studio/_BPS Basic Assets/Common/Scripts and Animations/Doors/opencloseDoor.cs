using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    public class opencloseDoor : MonoBehaviour
    {
        [SerializeField] private Animator openandclose;
        [SerializeField] private bool open;

        private bool isAnimating;

        private void Awake()
        {
            if (openandclose == null)
                openandclose = GetComponent<Animator>();

            if (openandclose == null)
                Debug.LogWarning($"Animator tidak ditemukan pada {name}", this);
        }

        public void Toggle()
        {
            if (openandclose == null || isAnimating)
                return;

            if (open)
                StartCoroutine(Animate("Closing", false));
            else
                StartCoroutine(Animate("Opening", true));
        }

        private IEnumerator Animate(string animationName, bool newOpenState)
        {
            isAnimating = true;
            openandclose.Play(animationName);
            open = newOpenState;

            yield return new WaitForSeconds(0.5f);

            isAnimating = false;
        }
    }
}