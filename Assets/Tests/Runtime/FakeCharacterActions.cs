#if UNITY_EDITOR   // test helper only: never compiled into a game build
using UnityEngine;

namespace GameJamTests
{
    /// <summary>
    /// Test stand-in for a character: counts the reactions it was asked to play.
    /// Lives outside an Editor folder because Unity won't attach MonoBehaviours from editor assemblies.
    /// </summary>
    public class FakeCharacterActions : MonoBehaviour, ICharacterActions
    {
        public int slaps, stungs, powerUps;
        public void Slap() => slaps++;
        public void Stung() => stungs++;
        public void PowerUp() => powerUps++;
        public bool IsDizzy => false;
    }
}
#endif
