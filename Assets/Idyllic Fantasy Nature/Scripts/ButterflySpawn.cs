using UnityEngine;

namespace IdyllicFantasyNature
{
    public class ButterflySpawn : MonoBehaviour
    {
        [Tooltip("child with the mesh renderer to make the butterfly invisible when the animation is not running")]
        [SerializeField] private GameObject _butterflyChild;
        // the spawn area script to get the data from the set range and cooldown
        private ButterflySpawnArea _area;
        // the cooldown to respawn the butterfly
        private float _cooldown = 0;
        // indicates if the animation is playing to stop the cooldown for the respawn
        private bool _isPlaying = false;
        // animator of the butterfly
        private Animator _animator;

        // Start is called before the first frame update
        void Start()
        {
            _animator = GetComponent<Animator>();
            _area = GetComponentInParent<ButterflySpawnArea>();

            if (_animator != null)
                _animator.enabled = false;

            if (_area != null)
                _cooldown = Random.Range(_area.MinCooldown, _area.MaxCooldown);

            if (_butterflyChild != null)
                _butterflyChild.SetActive(false);
        }

        // Update is called once per frame
        void Update()
        {
            ActivateButterfly();
        }


        /// <summary>
        /// activates the butterfly when the cooldown reaches 0 and the animation isn't already playing
        /// </summary>
        private void ActivateButterfly()
        {
            if (!_isPlaying)
            {
                if (_cooldown <= 0)
                {
                    if (_area == null || _area.Collider == null || _butterflyChild == null || _animator == null)
                        return;

                    // activates the animator to play the animation
                    _animator.enabled = true;
                    // resets the cooldown for the respawn
                    _cooldown = Random.Range(_area.MinCooldown, _area.MaxCooldown);
                    // makes the butterfly visible
                    _butterflyChild.SetActive(true);
                    // determines a random position for the butterfly in the spawn area
                    var b = _area.Collider.bounds;
                    transform.position = new Vector3(
                        Random.Range(b.min.x, b.max.x),
                        Random.Range(b.min.y, b.max.y),
                        Random.Range(b.min.z, b.max.z));

                    _isPlaying = true;

                }
                else
                {
                    _cooldown -= Time.deltaTime;
                }
            }
        }

        /// <summary>
        /// animation event will be used at the end of the butterfly animation
        /// Resets the stats to enable the cooldown for the respawn
        /// </summary>
        public void AnimationEnded()
        {
            _isPlaying = false;
            if (_animator != null)
                _animator.enabled = false;
            if (_butterflyChild != null)
                _butterflyChild.SetActive(false);
        }
    }
}
