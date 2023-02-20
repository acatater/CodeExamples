using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class Player : MonoBehaviour
{
    public static Action<float> OnMoneyChanged;

    [SerializeField]
    private float _rangeOfHacking;
    [SerializeField]
    private float _maxEnergy;

    private float _money;

    private bool _isAiming;
    private bool _isSearchingEnemy = false;

    [SerializeField]
    private EntityScriptableObject _entityData;

    [SerializeField]
    private Transform _playerCameraTarget, _playerCameraTargetSecond;

    private CharacterAnimation _characterAnim;
    private PlayerInput _playerInput;
    private Rigidbody2D _rigidbody2D;
    private WeaponManager _weaponManager;
    
    private EntityAttributies _entityAttributies;
    private EntitiesDetector _entitiesDetector;

    private Transform _targetPosition, _targetCameraPosition;

    private void OnEnable()
    {
        _entityAttributies.onEntityDie += StopAllCoroutines;
        EventManager.onExplosion += ShakeCameraOnExplosion;
    }

    private void OnDisable()
    {
        EventManager.onExplosion -= ShakeCameraOnExplosion;
        _entityAttributies.onEntityDie -= StopAllCoroutines;
    }

    void Awake()
    {
        findNearestEnemy = GetComponent<FindNearestEnemy>();
        _characterAnim = GetComponent<CharacterAnimation>();
        _rigidbody2D = GetComponent<Rigidbody2D>();

        _entityAttributies = GetComponent<EntityAttributies>();
        _weaponManager = GetComponent<WeaponManager>();

        _playerInput = FindObjectOfType<PlayerInput>();
    }

    void Start()
    {
        _entityAttributies.ApplyData(_characterAnim, _rigidbody2D, _entityData);
        _weaponManager.SetEnergy(_maxEnergy);

        _weaponManager.AddPermanentBonusChance(_entityData.permanentChanceOfCrit);

        StartCoroutine(StartWithDelay());
    }

    public IEnumerator StartWithDelay()
    {
        yield return new WaitForSecondsRealtime(1f);
        CameraScript.Instance.SetTarget(_playerCameraTarget);
        EventManager.Instance.CloseAllTabs();
        Time.timeScale = 1;

        DontDestroyOnLoad(gameObject);
    }

    public void ApplyStats(ImplantsData implant)
    {
        _entityAttributies.movementSpeed += implant.addativeSpeed;
    }

    void Update()
    {
        if (_weaponManager != null && !_entityAttributies.dead)
        {
            StartCoroutine(FindNearestEnemy());
            
            if (_targetPosition != null)
            {
                _weaponManager.HandleAiming(_targetPosition);
                _isAiming = true;
            }
            else
            {
                _isAiming = false;
            }
        }
        if (!_characterAnim.frozen)
            Move();
        else
            _rigidbody2D.velocity = Vector3.zero;

    }

    private void Move()
    {
        Vector2 input = JoystickMovement.Instance.MovementAmount;

        if(_playerInput.actions["Move"].ReadValue<Vector2>() != Vector2.zero)
        {
            input = _playerInput.actions["Move"].ReadValue<Vector2>();
        }

        if (_targetPosition != null || _targetCameraPosition != null)
        {
            if (_targetPosition != null)
            {
                var distance = Vector2.Distance(transform.position, _targetPosition.position);
                if (distance <= 12)
                {
                    CameraScript.Instance.SetTarget(_playerCameraTarget);

                    if (_targetPosition != null)
                        CameraScript.Instance.Zoom(Vector2.Distance(transform.position, _targetPosition.position));
                    else
                        CameraScript.Instance.DefaultZoom();

                    _playerCameraTarget.position = transform.position + new Vector3(GetAimmingInfo().x * distance / 2, GetAimmingInfo().y * distance / 2);
                }
                else
                {
                    CameraScript.Instance.DefaultZoom();
                    _playerCameraTarget.position = (Vector2)transform.position + new Vector2(input.x * _entityAttributies.movementSpeed / 2, input.y * _entityAttributies.movementSpeed / 2);
                }
            }
            else
            {
                _playerCameraTarget.position = (Vector2)transform.position + new Vector2(input.x * _entityAttributies.movementSpeed / 2, input.y * _entityAttributies.movementSpeed / 2);
            }
        }
        else
        {
            CameraScript.Instance.SetTarget(_playerCameraTarget);
            _playerCameraTarget.position = (Vector2)transform.position + new Vector2(input.x * _entityAttributies.movementSpeed / 2, input.y * _entityAttributies.movementSpeed / 2);
            CameraScript.Instance.DefaultZoom();
        }
        _rigidbody2D.velocity = input * _entityAttributies.movementSpeed;
        AnimateMovement();
    }

    //Animations and effects

    private void AnimateMovement()
    {
        if (!_isAiming)
        {
            if (_playerInput.actions["Move"].ReadValue<Vector2>() != Vector2.zero || JoystickMovement.Instance.MovementAmount != Vector2.zero)
            {
                _weaponManager.HandleAiming(_playerCameraTarget);

                if (_rigidbody2D.velocity.x < 0)
                {
                    _characterAnim.Flip(true, true);
                }
                else if (_rigidbody2D.velocity.x > 0)
                {
                    _characterAnim.Flip(false, true);
                }
            }
            else
            {
                //weaponManager.HandleAiming(transform);
                if (_rigidbody2D.velocity.x < 0)
                {
                    _characterAnim.Flip(true, false);
                }
                else if (_rigidbody2D.velocity.x > 0)
                {
                    _characterAnim.Flip(false, false);
                }
            }
        }
        else if (_targetPosition != null)
        {
            Vector3 moveDir = (_targetPosition.position - transform.position).normalized;
            if (moveDir.x < 0)
            {
                _characterAnim.Flip(true, true);
            }
            else if (moveDir.x > 0)
            {
                _characterAnim.Flip(false, true);
            }
        }

        if (_rigidbody2D.velocity.magnitude > 0f)
        {
            float speed = _rigidbody2D.velocity.magnitude;

            if (speed < 0.25f)
            {
                speed = .25f;
            }
            _characterAnim.PlayMoveAnim(true, speed);
        }
        else
        {
            _characterAnim.PlayMoveAnim(false, 1);
        }
    }

    private Vector3 GetAimmingInfo()
    {
        if (_targetPosition != null)
            return (_targetPosition.position - transform.position).normalized;
        else if(_targetCameraPosition != null)
            return (_targetCameraPosition.position - transform.position).normalized;
        else return Vector3.zero;
    }

    private void ShakeCameraOnExplosion(Transform targetExplosion, float explosionForce)
    {
        var distance = 1f;
        var currentDistance = Vector2.Distance(transform.position, targetExplosion.position);
        var newForce = explosionForce * Mathf.Clamp01(distance / currentDistance);
        Debug.Log("Explosion force: " + newForce);
        CameraShake.Instance.ShakeCamera(newForce, Random.Range(.5f, .7f));
    }

    //Money

    public float Money => _money;

    public void SetMoney(float amount)
    {
        _money = amount;

        OnMoneyChanged?.Invoke(_money);
    }

    public void AddMoney(float amount)
    {
        _money += amount;

        OnMoneyChanged?.Invoke(_money);
    }

    public void TakeMoney(float amount)
    {
        _money -= amount;

        OnMoneyChanged?.Invoke(_money);
    }



    private IEnumerator FindNearestEnemy()
    {
        if (_isSearchingEnemy)
            yield break;

        _isSearchingEnemy = true;

        List<Transform> tempTargets = _entitiesDetector.FindClosestTargets(_weaponManager.aimRange, "Enemy");

        if (tempTargets != null)
        {
            Transform closestTarget = null;
            Transform closestCameraTarget = null;

            float closestDistanceSqr = Mathf.Infinity;

            foreach (var currentTarget in tempTargets)
            {
                if (_entityAttributies.layerToIgnore == (_entityAttributies.layerToIgnore | (1 << currentTarget.gameObject.layer)))
                    continue;

                RaycastHit2D hit = Physics2D.Raycast(transform.position, (currentTarget.position - transform.position).normalized, _weaponManager.aimRange, ~_entityAttributies.layerToIgnore);

                //Debug.Log(hit.collider.name);
                var distance = Vector2.Distance(transform.position, currentTarget.position);

                if (closestCameraTarget == null)
                    _targetCameraPosition = currentTarget;

                if (closestTarget == null && (hit.collider != null && hit.collider.gameObject.CompareTag("Enemy")))
                    closestTarget = currentTarget;


                if (distance < closestDistanceSqr)
                {
                    closestCameraTarget = currentTarget;

                    if (hit.collider != null && hit.collider.gameObject.CompareTag("Enemy"))
                    {
                        closestDistanceSqr = distance;
                        closestTarget = currentTarget;
                    }
                }

            }

            if (closestTarget != null)
                _targetPosition = closestTarget;
            else
                _targetPosition = null;

            if (closestCameraTarget != null)
            {
                Debug.LogWarning(_targetCameraPosition.tag);

                _targetCameraPosition = closestCameraTarget;
            }
            else
                _targetCameraPosition = null;
        }
        else
        {
            _targetCameraPosition = null;
            _targetPosition = null;
        }

        yield return new WaitForSeconds(.1f);

        _isSearchingEnemy = false;

    }
}
