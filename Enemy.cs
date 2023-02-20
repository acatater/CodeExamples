using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Enemy : MonoBehaviour
{
    public static Action OnEnemyShoot;

    [SerializeField]
    private List<SteeringBehaviour> _steeringBehaviours;

    [SerializeField]
    private List<Detector> _detectors;

    [SerializeField]
    private AIData _aiData;

    [SerializeField]
    private ContextSolver _movementDirectionSolver;

    [SerializeField]
    private float _detectionDelay = 0.05f;

    [SerializeField]
    private int _attackDelay = 2;

    public float SearchRange;
    private float _aimRange, _newAimRange;

    //==   -----------   ==

    private bool _brainEnabled;

    private bool _searchingForRandomArea;
    private float _timeForNextMove;

    private float _currentJumpTimeCooldown, _currentJumpTime;
    private bool _canJumpNow, _isJumping;
    private Vector2 _lastSavedDirectionForJump;

    private bool _isAiming, _isShooting;
    private float _timeForNextShoot;

    private bool _reachedTarget = false;
    private bool _startedReachCoroutine = false;

    [SerializeField]
    private LayerMask _obstaclesLayerMask;

    [SerializeField]
    private float _colliderSize;

    [SerializeField]
    private Transform _colliderCenter;

    private Rigidbody2D _rigidbody2D;

    private WeaponManager _weaponManager;
    private CharacterAnimation _characterAnim;

    [SerializeField]
    private WeaponScriptableObject _weapon;
    [SerializeField]
    private EntityScriptableObject _enemyData;

    private EntityAttributies _entityAttributies;

    [SerializeField]
    private Collider2D _mainCollider;

    void Awake()
    {
        _weaponManager = GetComponent<WeaponManager>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _characterAnim = GetComponent<CharacterAnimation>();
        //weaponParent = transform.Find("WeaponParent");
        _entityAttributies = GetComponent<EntityAttributies>();

        if (_enemyData.enemyType == EnemyType.kamikaze)
        {
            var randomDamage = Random.Range(40, 120);
            _entityAttributies.onEntityDie += Explode;
        }
        if (_enemyData != null)
        {
            _entityAttributies.ApplyData(_characterAnim, _rigidbody2D, _enemyData);
            SearchRange = _enemyData.searchRange;
        }

        _entityAttributies.onEntityDie += PerformDetection;

        OnEnemyShoot += AddTimeBetweenShots;
    }

    private void OnDisable()
    {
        _entityAttributies.onEntityDie -= PerformDetection;
        _entityAttributies.onEntityDie -= Explode;
        OnEnemyShoot -= AddTimeBetweenShots;
    }

    private void Start()
    {
        if (_enemyData != null)
            ApplyData();
        //Detecting Player and Obstacles around
        InvokeRepeating("PerformDetection", 0, _detectionDelay);

        _timeForNextShoot = _attackDelay;
        _newAimRange = _aimRange + Random.Range(0, 3);
    }
    private void PerformDetection()
    {
        foreach (Detector detector in _detectors)
        {
            detector.Detect(_aiData);
        }
    }

    void FixedUpdate()
    {
        if (_enemyData.enemyType == EnemyType.kamikaze)
        {
            if (_aiData.currentTarget != null && _aiData.currentTarget.CompareTag("Player"))
            {
                if (Vector2.Distance(_aiData.currentTarget.position, transform.position) < 1f)
                {
                    _entityAttributies.Die();
                }
            }
        }

        if (_brainEnabled && !_characterAnim.frozen)
        {
            Move();
            if (_currentJumpTimeCooldown < _enemyData.jumpCouldown && !_canJumpNow)
            {
                _currentJumpTimeCooldown += Time.fixedDeltaTime;
                _currentJumpTime = 1.1f;
            }
            else
                _canJumpNow = true;
        }
        if (!_isAiming)
        {
            _weaponManager.HandleAiming(null);
        }
        if (_enemyData.canJump)
        {
            if (_canJumpNow)
            {
                if (_currentJumpTime > 0)
                {
                    _currentJumpTime -= Time.fixedDeltaTime;
                    JumpToTarget();
                    Debug.Log("WTF");
                    _characterAnim.PlayJumpAnim(true);
                    _isJumping = true;
                }

                else
                {
                    Debug.Log("WTF3");
                    _lastSavedDirectionForJump = Vector2.zero;
                    _currentJumpTimeCooldown = 0;
                    _characterAnim.PlayJumpAnim(false);
                    _canJumpNow = false;
                    _isJumping = false;
                    //entityAttributies.movementSpeed = defaultSpeed;
                }
            }
        }
        if (_timeForNextShoot > 0)
        {
            _timeForNextShoot -= Time.deltaTime;
        }
    }
    private void Move()
    {
        if (_aiData.currentTarget != null)
        {
            _isAiming = true;

            float distance = Vector2.Distance(_aiData.currentTarget.position, transform.position);
            Vector3 direction = (_aiData.currentTarget.position - transform.position).normalized;

            RaycastHit2D hit = Physics2D.Raycast(transform.position + direction / 2, direction, SearchRange, ~_entityAttributies.layerToIgnore);
            if (hit != false)
            {
                if (distance < _aimRange && hit.collider.gameObject.CompareTag("Player"))
                {
                    //ShootTheTarget
                    ShootTarget();
                    RandomMovement(0f, 1f, true);
                }
                else if ((distance > _aimRange && hit.collider.gameObject.CompareTag("Player")) || _movementDirectionSolver.GetDirectionToMove(_steeringBehaviours, _aiData) != Vector2.zero)
                {
                    //Go to target
                    GoToTarget();
                    if (distance < _newAimRange)
                        ShootTarget();
                    else 
                        StopShooting();
                }
                else
                {
                    StopShooting();
                }

            }
            else if (_movementDirectionSolver.GetDirectionToMove(_steeringBehaviours, _aiData) != Vector2.zero)
            {
                StopShooting();

                GoToTarget();
            }
        }
        else if (_aiData.GetTargetsCount() > 0)
        {
            //Target acquisition logic
            _aiData.currentTarget = _aiData.targets[0];
        }
        if (_aiData.currentTarget == null)
        {
            _reachedTarget = true;
            StopShooting();
            _isAiming = false;
        }

        AnimateMovement();

    }

    [Button("Turn on AI")]
    public void SwitchBrainAI(bool enabled)
    {
        if (enabled)
        {
            TurnOnColliders();
            _brainEnabled = true;
        }
        if (!enabled)
        {
            TurnOffColliders();
            _brainEnabled = false;
        }
    }

    public void TurnOffColliders()
    {
        gameObject.layer = 7;
        _mainCollider.gameObject.layer = 7;
    }

    public void TurnOnColliders()
    {
        gameObject.layer = 9;
        _mainCollider.gameObject.layer = 9;
    }

    private void GoToTarget()
    {
        _aiData.randomMovement = false;

        if (!_isJumping)
        {
            //isAiming = false;
            Vector3 moveDir = _movementDirectionSolver.GetDirectionToMove(_steeringBehaviours, _aiData);
            _weaponManager.HandleAiming(_aiData.currentTarget);
            _lastSavedDirectionForJump = moveDir;
            _rigidbody2D.velocity = moveDir * _entityAttributies.movementSpeed;

            if (!_startedReachCoroutine)
                StartCoroutine(CheckIfReachedTarget());

        }

    }

    private IEnumerator CheckIfReachedTarget()
    {
        _startedReachCoroutine = true;
        _reachedTarget = false;

        yield return new WaitForSeconds(5);

        _startedReachCoroutine = false;

        if (_reachedTarget == false)
        {
            _aiData.currentTarget = null;
        }
    }
    private void JumpToTarget()
    {
        Debug.Log("Jump");

        _aiData.randomMovement = false;

        _rigidbody2D.AddForce(_lastSavedDirectionForJump * _enemyData.jumpForce, ForceMode2D.Impulse);
    }

    private void ShootTarget()
    {
        _isAiming = true;
        _weaponManager.HandleAiming(_aiData.currentTarget);

        if (_colliderCenter != null && _aiData.currentTarget != null)
        {
            Vector3 direction = (_aiData.currentTarget.position - _colliderCenter.position).normalized;

            RaycastHit2D hitEnemy = Physics2D.Raycast(_colliderCenter.position + direction * _colliderSize, direction, SearchRange, ~7);

            Debug.DrawRay(_colliderCenter.position + direction * _colliderSize, direction, Color.red);

            if (hitEnemy.collider != null && hitEnemy.collider.CompareTag("Enemy"))
            {
                //Debug.Log("I see enemy, I shouldn't shoot!");
                return;
            }

        }

        if (_weapon != null && _timeForNextShoot <= 0)
        {
            _timeForNextShoot = Random.Range(0, _attackDelay);
            _isShooting = true;
            _weaponManager.HandldeShooting();
            OnEnemyShoot?.Invoke();
        }
    }

    private void StopShooting()
    {
        if (_isShooting)
        {
            _isShooting = false;
            _weaponManager.StopShooting();
        }
        RandomMovement(0f, 2f, false);
    }

    private void RandomMovement(float minTime, float maxTime, bool isMovingWhileSearch)
    {
        //Vector3 moveDir = Vector3.zero;

        if (_isJumping)
            return;

        Vector2 moveDir = _movementDirectionSolver.GetDirectionToMove(_steeringBehaviours, _aiData, true);

        if (!isMovingWhileSearch && Vector3.Distance(_rigidbody2D.velocity, transform.position) <= 0)
            _rigidbody2D.velocity = Vector3.zero;

        if (_searchingForRandomArea == false)
        {
            _timeForNextMove = Random.Range(minTime, maxTime);

            _aiData.randomMovement = true;

            moveDir = _movementDirectionSolver.GetDirectionToMove(_steeringBehaviours, _aiData, true);

            _searchingForRandomArea = true;
        }

        if (_timeForNextMove >= 0)
            _timeForNextMove -= Time.deltaTime;
        else
        {
            //Debug.Log("Should move");
            _rigidbody2D.velocity = moveDir * _entityAttributies.movementSpeed;
            _searchingForRandomArea = false;
        }
    }

    private void AnimateMovement()
    {
        if (_isAiming && _aiData.currentTarget != null)
        {
            _weaponManager.HandleAiming(_aiData.currentTarget);

            Vector3 moveDir = (_aiData.currentTarget.position - transform.position).normalized;
            if (moveDir.x < 0)
            {
                _characterAnim.Flip(true, true);
            }
            else if (moveDir.x > 0)
            {
                _characterAnim.Flip(false, true);
            }
        }
        else if (!_isAiming)
        {
            if (_rigidbody2D.velocity.x < 0)
            {
                _characterAnim.Flip(true, false);
            }
            else if (_rigidbody2D.velocity.x > 0)
            {
                _characterAnim.Flip(false, false);
            }
        }

        if (_rigidbody2D.velocity.magnitude > 0f)
        {
            float speed = _rigidbody2D.velocity.magnitude;

            if (speed < 0.25f)
            {
                speed = .25f;
            }
            if (!_isJumping)
                _characterAnim.PlayMoveAnim(true, speed);
        }
        else
            _characterAnim.PlayMoveAnim(false, 1);
    }

    private void Explode()
    {
        EventManager.Explode(transform, 1f, 70, _enemyData.damageFromExplosion, _enemyData.damageTypeFromExlposion, 0);
    }

    private void ApplyData()
    {
        if (_weapon != null)
        {
            _weaponManager.EquipWeapon(_weapon);
        }
        else
        {
            _weapon = _enemyData.defaultWeapon;
            _weaponManager.EquipWeapon(_weapon);
        }

        SearchRange = _enemyData.searchRange;
        _aimRange = Random.Range(_enemyData.aimRange.x, _enemyData.aimRange.y);
        _characterAnim.ChangeAnimator(_enemyData.animator);
        //defaultSpeed = enemyData.movementSpeed;
    }

    private void AddTimeBetweenShots()
    {
        //Debug.LogWarning("Added time");
        if(_timeForNextShoot < 1 && _brainEnabled)
            _timeForNextShoot += Random.Range(.2f, 1f);
    }

}
