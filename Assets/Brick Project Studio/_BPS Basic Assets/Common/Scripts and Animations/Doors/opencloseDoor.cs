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
        public float interactDistance = 3f;
        public Key interactKey = Key.E;

        private void Start()
        {
            if (openandclose == null)
                openandclose = GetComponent<Animator>();
        }

        private void Update()
        {
            // In the main scene the player controller chooses one door in front
            // of the player. Avoid every nearby door responding to the same key.
            if (global::PlayerDoorInteraction.ActivePlayer != null)
                return;

            if (Player == null || Keyboard.current == null ||
                !Keyboard.current[interactKey].wasPressedThisFrame)
                return;

            if (Vector3.Distance(Player.position, transform.position) <= interactDistance &&
                IsClosestDoorForPlayer())
            {
                ToggleDoor();
            }
        }

        private bool IsClosestDoorForPlayer()
        {
            opencloseDoor[] doors = UnityEngine.Object.FindObjectsByType<opencloseDoor>(FindObjectsSortMode.None);
            opencloseDoor closest = null;
            float closestDistance = float.MaxValue;

            foreach (opencloseDoor door in doors)
            {
                if (door == null || door.Player != Player || door.openandclose == null)
                    continue;

                float distance = Vector3.Distance(Player.position, door.transform.position);
                if (distance <= door.interactDistance && distance < closestDistance)
                {
                    closest = door;
                    closestDistance = distance;
                }
            }

            return closest == this;
        }

        public void ToggleDoor()
        {
            SetOpen(!open);
        }

        public void SetOpen(bool shouldOpen)
        {
            if (openandclose == null || open == shouldOpen)
                return;

            open = shouldOpen;
            openandclose.Play(open ? "Opening" : "Closing", 0, 0f);

        }
    }
}
