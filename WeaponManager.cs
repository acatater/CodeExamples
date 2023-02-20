using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class WeaponManager : MonoBehaviour
{
    public static Action OnStopShooting;
    public static Action<float> OnEnergyChanged;

    [SerializeField] private LayerMask _targetLayer;
    private string _ignoreTag;

    public Transform AimTransform, WeaponEndTransform, ShootPointTransform;
    
    private float _currentEnergy, _maxEnergy;

    public float AimRange = 1f;

    [SerializeField] private float _defaultDamage, _defaultFirerate;

    private float _damage, _firerate, _spread, _timeBetweenShots;
    private float _addativeCritChance, _permanentCritChance;

    private int _bulletAmount, _bulletsLeft;


    private bool _allowNullWeaponFire = true;

    private bool _startedAttackWithDelay = false;
    private bool _changedLineWidth = true;

    
    private List<WeaponSlot> _weaponsList = new();

    private int _currentWeaponSlot = 0;
    [SerializeField] [Range(0, 4)]
    private int _maxWeaponsSlots = 2;

    [SerializeField]
    private WeaponScriptableObject _nullWeapon;

    private bool _flippedParticleEffect;

    [SerializeField]
    private Animator _weaponAnimator;
    private SpriteRenderer _weaponSprite;
    private CharacterAnimation _characterAnim;

    private WeaponScriptableObject _equippedWeapon;
    private CharType _charType;

    private Transform _targetTransform;

    private LineRenderer _lineRenderer;
    private LayerMask _layerToIgnore;

    void Awake()
    {
        SetAllData();
    }

    private void OnEnable()
    {
        if (_charType == CharType.player)
        {
            EventManager.onButtonPressedButtonSwitchWeapons += SwitchWeapon;
            EventManager.onButtonPressedFire += HandldeShooting;
            EventManager.onButtonFireReleased += StopShooting;
        }
    }

    private void OnDisable()
    {
        if (_charType == CharType.player)
        {
            EventManager.onButtonPressedButtonSwitchWeapons -= SwitchWeapon;
            EventManager.onButtonPressedFire -= HandldeShooting;
            EventManager.onButtonFireReleased -= StopShooting;
        }
    }
    
    private void SetAllData()
    {
        if (AimTransform == null)
            AimTransform = transform.Find("WeaponParent");
        _lineRenderer = transform.Find("WeaponParent").GetComponent<LineRenderer>();
        _characterAnim = GetComponent<CharacterAnimation>();

        if (WeaponEndTransform == null)
            WeaponEndTransform = CustomFindChild("WeaponEndPosition", transform);
        if (ShootPointTransform == null)
            ShootPointTransform = CustomFindChild("ShootPosition", transform);

        if (_weaponAnimator == null)
            _weaponAnimator = CustomFindChild("WeaponSprite", transform).GetComponent<Animator>();
        _weaponSprite = CustomFindChild("WeaponSprite", transform).GetComponent<SpriteRenderer>();

        _ignoreTag = gameObject.tag;
        if (_equippedWeapon == null) _weaponSprite.sprite = null;
    }

    public void AddPermanentBonusChance(float value)
    {
        _permanentCritChance += value;
    }

    public void SetWeaponEndPosition(Vector3 position)
    {
        WeaponEndTransform.localPosition = position;
    }

    public void SetCharType(CharType charType)
    {
        this._charType = charType;

        if (charType == CharType.player)
        {
            EventManager.onButtonPressedButtonSwitchWeapons += SwitchWeapon;
            EventManager.onButtonPressedFire += HandldeShooting;
            EventManager.onButtonFireReleased += StopShooting;
        }
        else
        {
            _maxEnergy = float.PositiveInfinity;
            _currentEnergy = _maxEnergy;
        }
    }

    public void SetLayerToIgnore(LayerMask layer)
    {
        _layerToIgnore = layer;
    }
    public void SetEnergy(float maxEnergy)
    {
        if (_charType == CharType.player)
        {
            this._maxEnergy = maxEnergy;
            _currentEnergy = maxEnergy;
        }
    }

    public bool AddEnergy(float energy)
    {
        if(_maxEnergy <= _currentEnergy)
        {
            return false;
        }

        if (_maxEnergy > _currentEnergy)
        {
            _currentEnergy += energy;
        }

        if (_maxEnergy < _currentEnergy)
        {
            _currentEnergy = _maxEnergy;
        }
        OnEnergyChanged?.Invoke(_currentEnergy);
        return true;
    }

    public float GetMaxEnergy => _maxEnergy;

    public WeaponScriptableObject GetCurrentWeapon() => _equippedWeapon;

    public void EquipWeapon(WeaponScriptableObject weaponData)
    {
        if (weaponData != null)
        {
            if (_charType.Equals(CharType.player))
            {
                //allowFirstGunFire = true;
                var allDiscoveredWeapons = GameHandler.Instance.GetDiscoveredWeapons();
                if (!allDiscoveredWeapons.Contains(weaponData) && WeaponAsset.Instance.GetAllWeapons().Contains(weaponData) && weaponData.canBeDiscovered)
                {
                    GameHandler.Instance.AddAndSaveWeaponDiscoveredData(weaponData);
                }
            }
            if (_equippedWeapon != null)
            {
                if (_weaponsList.Count == _maxWeaponsSlots)
                {
                    DropWeapon();

                    var newWeapon = new WeaponSlot();

                    newWeapon.SetWeapon(weaponData);

                    _weaponsList.Add(newWeapon);

                    //Debug.Log(weapons[currentWeaponSlot].weapon.name);

                    _currentWeaponSlot = _weaponsList.Count - 1;

                    _equippedWeapon = _weaponsList[_weaponsList.Count - 1].weapon;
                    ApplyWeaponData();
                }
                else
                {
                    var newWeapon = new WeaponSlot();

                    newWeapon.SetWeapon(weaponData);

                    _weaponsList.Add(newWeapon);

                    if (_currentWeaponSlot < _weaponsList.Count - 1)
                    {
                        _currentWeaponSlot += 1;
                    }
                    else if (_currentWeaponSlot == _weaponsList.Count - 1)
                    {
                        _currentWeaponSlot = 0;
                    }

                    _equippedWeapon = _weaponsList[_weaponsList.Count - 1].weapon;
                    ApplyWeaponData();
                }
            }
            else
            {
                var newWeapon = new WeaponSlot();

                newWeapon.SetWeapon(weaponData);

                _weaponsList.Add(newWeapon);

                _equippedWeapon = _weaponsList[0].weapon;
            }
        }

        ApplyWeaponData();

    }

    public void SetNullWeapon(WeaponScriptableObject weapon)
    {
        _nullWeapon = weapon;
    }

    public void SwitchWeapon()
    {
        if (_weaponsList.Count <= 1)
            return;

        if(_currentWeaponSlot < _weaponsList.Count - 1)
        {
            _currentWeaponSlot += 1;
        }
        else if(_currentWeaponSlot == _weaponsList.Count - 1)
        {
            _currentWeaponSlot = 0;
        }

        _equippedWeapon = _weaponsList[_currentWeaponSlot].weapon;

        ApplyWeaponData();

        /*
        if (previousWeapon != null)
        {
            switched = !switched;
            WeaponScriptableObject tempWeapon = previousWeapon;
            previousWeapon = equippedWeapon;
            equippedWeapon = tempWeapon;
            ApplyWeaponData();
        }
        */
    }
    private void DropWeapon()
    {
        if (_equippedWeapon.droppedPrefabWeapon != null)
        {
            var droppedWeapon = Instantiate(_equippedWeapon.droppedPrefabWeapon, transform.position, Quaternion.identity);
            droppedWeapon.GetComponent<WeaponPickup>().SetWeaponData(_equippedWeapon);
        }
        else
            Debug.LogWarning("Weapon-Drop prefab is not set");

        if (_weaponsList.Count > 0)
        {
            Debug.Log(_currentWeaponSlot);

            _weaponsList.Remove(_weaponsList[_currentWeaponSlot]);

            _currentWeaponSlot -= 1;

            if (_currentWeaponSlot < 0)
            {
                _currentWeaponSlot = 0;
            }

            if (_weaponsList.Count > 0 && _weaponsList[_currentWeaponSlot].weapon != null)
                _equippedWeapon = _weaponsList[_currentWeaponSlot].weapon;
            else
            {
                //Debug.Log("Should be null");
                _equippedWeapon = null;
            }
            /*
                equippedWeapon = previousWeapon;
            else
                equippedWeapon = null;
            */
            ApplyWeaponData();
        }
    }
    private void ApplyWeaponData()
    {
        StopShooting();
        if (AimTransform != null && WeaponEndTransform != null)
        {
            if (_equippedWeapon != null)
            {
                if (_equippedWeapon.customWeapon)
                {
                    ApplyCustomWeaponData();
                }
                _damage = _equippedWeapon.damage;
                _firerate = _equippedWeapon.firerate;
                AimRange = _equippedWeapon.aimRange;               

                if (_weaponSprite != null)
                    _weaponSprite.sprite = _equippedWeapon.weaponSprite;

                if (_equippedWeapon.weaponType == WeaponType.firegun || _equippedWeapon.weaponType == WeaponType.rocket || _equippedWeapon.weaponType == WeaponType.shootgun)
                {
                    _spread = _equippedWeapon.spread;
                    _timeBetweenShots = _equippedWeapon.timeBetweenShots;
                    _bulletAmount = _equippedWeapon.bulletAmount;
                    _bulletsLeft = _bulletAmount;

                    WeaponEndTransform.localEulerAngles = new Vector3(WeaponEndTransform.localEulerAngles.x, WeaponEndTransform.localEulerAngles.y, 0);

                }
                if (_equippedWeapon.weaponType == WeaponType.melee)
                {
                    if (_flippedParticleEffect)
                        WeaponEndTransform.localEulerAngles = new Vector3(WeaponEndTransform.localEulerAngles.x, WeaponEndTransform.localEulerAngles.y, +_equippedWeapon.weaponRotationZ);
                    else
                        WeaponEndTransform.localEulerAngles = new Vector3(WeaponEndTransform.localEulerAngles.x, WeaponEndTransform.localEulerAngles.y, -_equippedWeapon.weaponRotationZ);
                }
                if (_equippedWeapon.weaponType == WeaponType.laser)
                {
                    _lineRenderer.startColor = _equippedWeapon.colorOfLaserStart;
                    _lineRenderer.endColor = _equippedWeapon.colorOfLaserEnd;

                    _lineRenderer.widthCurve = _equippedWeapon.wideOfLaser;
                }

                WeaponEndTransform.localPosition = _equippedWeapon.gunEndPosition;
            }
            else
            {
                _weaponSprite = null;
                _damage = _defaultDamage;
                _firerate = _defaultFirerate;
                AimTransform.gameObject.transform.rotation = Quaternion.identity;
            }
        }
        else
        {
            SetAllData();
            ApplyWeaponData();
        }
    }

    public void HandleAiming(Transform targetTransform)
    {
        this._targetTransform = targetTransform;

        if (!_characterAnim.frozen && _equippedWeapon != null)
        {
            if (this._targetTransform != null)
            {
                //var turnSpeed = 20f;

                Vector3 aimDirection = (this._targetTransform.position - transform.position).normalized;

                float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

                //Debug.Log("aimTransformRotation: " + aimTransform.eulerAngles.z + "angle: " + angle);

                if (_equippedWeapon != null && _equippedWeapon.weaponType == WeaponType.melee)
                {
                    if (!_characterAnim.flipped)
                    {
                        AimTransform.eulerAngles = new Vector3(0, 0, angle + _equippedWeapon.weaponRotationZ);
                    }
                    else
                    {
                        AimTransform.eulerAngles = new Vector3(0, 0, angle - _equippedWeapon.weaponRotationZ);
                    }
                }
                else
                    AimTransform.eulerAngles = new Vector3(0, 0, angle);//aimTransform.eulerAngles = Vector3.Slerp(aimTransform.eulerAngles, Quaternion.Euler(new Vector3(0, 0, angle)).eulerAngles, Time.deltaTime * turnSpeed);//aimTransform.eulerAngles = new Vector3(0, 0, Mathf.Lerp(aimTransform.eulerAngles.z, angle, Time.deltaTime * turnSpeed));

                Vector3 aimLocalScale = Vector3.one;
                if (angle > 90 || angle < -90)
                {
                    aimLocalScale.y = -1f;
                    if (_equippedWeapon != null && _equippedWeapon.weaponType == WeaponType.melee)
                        AimTransform.eulerAngles = new Vector3(0, 0, angle - _equippedWeapon.weaponRotationZ);
                    _characterAnim.Flip(true, true);
                    _flippedParticleEffect = true;
                }
                else
                {
                    aimLocalScale.y = +1f;
                    if (_equippedWeapon != null && _equippedWeapon.weaponType == WeaponType.melee)
                        AimTransform.eulerAngles = new Vector3(0, 0, angle + _equippedWeapon.weaponRotationZ);
                    _characterAnim.Flip(false, true);
                    _flippedParticleEffect = false;
                }
                //characterAnim.flipped = false;
                AimTransform.localScale = aimLocalScale;
            }
            else
            {
                if (_equippedWeapon != null)
                {
                    if (_equippedWeapon.weaponType == WeaponType.melee)
                    {
                        _flippedParticleEffect = false;
                        if (!_characterAnim.flipped)
                            AimTransform.eulerAngles = new Vector3(AimTransform.localEulerAngles.x, AimTransform.localEulerAngles.y, _equippedWeapon.weaponRotationZ);
                        else
                            AimTransform.eulerAngles = new Vector3(AimTransform.localEulerAngles.x, AimTransform.localEulerAngles.y, -_equippedWeapon.weaponRotationZ);
                    }
                    else
                    {

                        AimTransform.eulerAngles = Vector3.zero;


                    }
                }
            }
        }
    }

    public void HandldeShooting()
    {
        if (_nullWeapon != null && _targetTransform != null && !_targetTransform.CompareTag("CameraTarget"))
        {
            if (_charType.Equals(CharType.player) && Vector2.Distance(_targetTransform.position, transform.position) < _nullWeapon.aimRange)
            {
                if (_equippedWeapon != null)
                {
                    if (!(_equippedWeapon.weaponType.Equals(WeaponType.melee) && _currentEnergy - _equippedWeapon.energyLose >= 0))
                    {
                        AttackMelee();
                        return;
                    }
                }
                else
                {
                    AttackMelee();
                    return;
                }
                
            }
        }
        else if (_charType.Equals(CharType.player) && _nullWeapon == null)
            Debug.LogWarning("Null weapon is not set, can't use second attack");

        if (_equippedWeapon != null)
        {
            if (_currentEnergy - _equippedWeapon.energyLose >= 0)
            {
                if(_weaponsList[_currentWeaponSlot].allowFire)
                {
                    Debug.Log(_weaponsList[_currentWeaponSlot].weapon.name);

                    AnimateShooting();
                    Attack();


                    if (_equippedWeapon.weaponType != WeaponType.laser)
                    {
                        TakeEnergy(_equippedWeapon.energyLose);
                        StartCoroutine(_weaponsList[_currentWeaponSlot].WaitToFireAgain());
                    }
                }
            }
        }
    }

    public void TakeEnergy(float energy)
    {
        _currentEnergy -= energy;

        if (_charType == CharType.player)
        {
            OnEnergyChanged?.Invoke(_currentEnergy);
        }
    }

    public void StopShooting()
    {
        if (_equippedWeapon != null)
        {
            StopCoroutine(ShootBullet(0, 0));
            if (_equippedWeapon.fireAnimation != null)
                _weaponAnimator.ResetTrigger(_equippedWeapon.fireAnimation.name);
            _weaponAnimator.SetTrigger("Stop");
            if (_equippedWeapon.weaponType == WeaponType.laser)
            {
                _lineRenderer.widthMultiplier = 0;
            }
        }
    }

    private void AnimateShooting()
    {
        if (_equippedWeapon != null)
        {
            if (_equippedWeapon.weaponAttackParticleSystem != null)
            {
                var psef = Instantiate(_equippedWeapon.weaponAttackParticleSystem, WeaponEndTransform.position, WeaponEndTransform.rotation);

                if (_equippedWeapon.weaponType == WeaponType.melee && _characterAnim.flipped)
                {
                    psef.GetComponent<ParticleSystemRenderer>().flip = new Vector3(0, 1, 0);
                }

                psef.transform.localEulerAngles = new Vector3(psef.transform.localEulerAngles.x, psef.transform.localEulerAngles.y, psef.transform.localEulerAngles.z - 180);
                
                Destroy(psef, 1f);
            }
            if (_equippedWeapon.fireAnimation != null)
                _weaponAnimator.SetTrigger(_equippedWeapon.fireAnimation.name);
            _weaponAnimator.ResetTrigger("Stop");
        }

    }

    private IEnumerator WaitForNextShotForNullGun()
    {
        yield return new WaitForSeconds(_nullWeapon.firerate);
        _allowNullWeaponFire = true;
    }

    private void Attack()
    {

        if (_equippedWeapon != null)
        {
            if (_charType == CharType.player)
            {
                if (!_equippedWeapon.fullyAuto)
                    OnStopShooting?.Invoke();
            }

            if (_equippedWeapon.weaponType == WeaponType.firegun || _equippedWeapon.weaponType == WeaponType.rocket)
            {
                StartCoroutine(ShootBullet(_timeBetweenShots, _bulletAmount));
            }

            if (_equippedWeapon.weaponType == WeaponType.shootgun)
            {
                for (int i = 0; i < _bulletAmount; i++)
                {

                    StartCoroutine(ShootBullet(_timeBetweenShots, 1));
                }
            }

            if (_equippedWeapon.weaponType == WeaponType.laser)
            {
                if (_changedLineWidth)
                    StartCoroutine(ChangeLineWidth());

                _lineRenderer.widthMultiplier = 1;
                //lineRenderer.startWidth = Random.Range(lineRenderer.startWidth, lineRenderer.startWidth + 0.1f);
                if (_targetTransform != null && !_targetTransform.CompareTag("CameraTarget"))
                {
                    _lineRenderer.SetPosition(1, _targetTransform.position);
                    if (!_startedAttackWithDelay)
                    {
                        _startedAttackWithDelay = true;
                        if (_currentEnergy - _equippedWeapon.energyLose >= 0)
                            StartCoroutine(AttackWithDelay());
                    }
                }
                else
                {
                    Vector2 targetPosition = Vector2.zero;

                    //if (!characterAnim.flipped)
                    if (_targetTransform == null)
                    {
                        return;
                    }

                    Vector2 direction = (_targetTransform.position - transform.position).normalized;
                    RaycastHit2D hit = Physics2D.Raycast(WeaponEndTransform.position, (_targetTransform.position - transform.position).normalized, AimRange, ~_layerToIgnore);
                    if (hit.collider != null)
                        targetPosition = hit.point;
                    else
                    {
                        targetPosition = new Vector2(WeaponEndTransform.position.x + (direction.x * AimRange), WeaponEndTransform.position.y + (direction.y * AimRange));
                    }

                    _lineRenderer.SetPosition(1, targetPosition);
                }
                if (_currentEnergy - _equippedWeapon.energyLose <= 0)
                    StopShooting();
            }
                
            if (_equippedWeapon.weaponType == WeaponType.melee)
            {
                if (_equippedWeapon.pfBullet == null)
                {
                    Debug.LogWarning("Bullet prefab is not set");
                    return;
                }
                GameObject bulletTransform = Instantiate(_equippedWeapon.pfBullet, WeaponEndTransform.position, Quaternion.identity);
                Physics2D.IgnoreCollision(bulletTransform.GetComponent<Collider2D>(), GetComponent<Collider2D>());
                Bullet bullet = bulletTransform.GetComponent<Bullet>(); ;
                Vector3 attackDir = (ShootPointTransform.position - WeaponEndTransform.position).normalized;

                float newDamage = GetCritDamageWithChance(_equippedWeapon);

                bullet.Setup(attackDir, newDamage, _equippedWeapon.damageType, _ignoreTag, _equippedWeapon.speedOfBullet, _equippedWeapon.chanceOfEffect);
                Destroy(bulletTransform, 0.1f);
            }
        }


    }

    private IEnumerator ChangeLineWidth()
    {
        _changedLineWidth = false;
        _lineRenderer.SetPosition(0, new Vector2(Random.Range(WeaponEndTransform.position.x - 0.01f, WeaponEndTransform.position.x + 0.01f), Random.Range(WeaponEndTransform.position.y - 0.01f, WeaponEndTransform.position.y + 0.01f)));
        yield return new WaitForSecondsRealtime(0.005f);
        _changedLineWidth = true;
    }

    private IEnumerator AttackWithDelay()
    {
        IDamagable damagable = _targetTransform.GetComponent<IDamagable>();
        if (damagable == null)
            damagable = _targetTransform.GetComponentInParent<IDamagable>();

        float newDamage = GetCritDamageWithChance(_equippedWeapon);

        bool damageFromPlayer = false;
        if (_ignoreTag == "Player")
            damageFromPlayer = true;

        damagable.Damage(newDamage, Vector3.zero, _equippedWeapon.damageType, _equippedWeapon.chanceOfEffect, false, damageFromPlayer);
        TakeEnergy(_equippedWeapon.energyLose);
        yield return new WaitForSeconds(_equippedWeapon.firerate);
        _startedAttackWithDelay = false;
    }

    public void AddTempCritChance(float value, float waitTime)
    {
        StartCoroutine(AddativeCritChance(value, waitTime));
    }

    private IEnumerator AddativeCritChance(float value, float waitTime)
    {
        _addativeCritChance += value;

        yield return new WaitForSeconds(waitTime);

        _addativeCritChance -= value;
    }

    private void AttackMelee()
    {
        OnStopShooting?.Invoke();

        StartCoroutine(WaitForNextShotForNullGun());

        if (_nullWeapon.pfBullet == null)
        {
            Debug.LogWarning("Bullet prefab is not set");
            return;
        }

        Vector3 attackDir = (ShootPointTransform.position - WeaponEndTransform.position).normalized;

        var psef = Instantiate(_nullWeapon.weaponAttackParticleSystem, transform.position + (attackDir / 2), WeaponEndTransform.rotation);
        if (_characterAnim.flipped)
        {
            psef.GetComponent<ParticleSystemRenderer>().flip = new Vector3(0, 1, 0);
        }

        psef.transform.localEulerAngles = new Vector3(psef.transform.localEulerAngles.x, psef.transform.localEulerAngles.y, psef.transform.localEulerAngles.z - 180);
        
        Destroy(psef, 1f);

        GameObject bulletTransform = Instantiate(_nullWeapon.pfBullet, transform.position + (attackDir / 2), Quaternion.identity);
        Physics2D.IgnoreCollision(bulletTransform.GetComponent<Collider2D>(), GetComponent<Collider2D>());
        Bullet bullet = bulletTransform.GetComponent<Bullet>(); ;

        float newDamage = GetCritDamageWithChance(_nullWeapon);

        bullet.Setup(attackDir, newDamage, _nullWeapon.damageType, _ignoreTag, _nullWeapon.speedOfBullet, _nullWeapon.chanceOfEffect);
        Destroy(bulletTransform, 0.1f);

        StopShooting();
    }

    private IEnumerator ShootBullet(float timeBetweenShots, int bulletsAmount)
    {
        if (_equippedWeapon.pfBullet == null)
        {
            Debug.LogWarning("Bullet prefab is not set");
            yield break;
        }

        float randomSpread = Random.Range(-_spread, _spread);
        Vector3 attackDir = (new Vector3(ShootPointTransform.position.x + randomSpread, ShootPointTransform.position.y + randomSpread) - WeaponEndTransform.position).normalized;
        GameObject bulletTransform = Instantiate(_equippedWeapon.pfBullet, WeaponEndTransform.position, Quaternion.identity);
        Physics2D.IgnoreCollision(bulletTransform.GetComponent<Collider2D>(), GetComponent<Collider2D>());
        Bullet bullet = bulletTransform.GetComponent<Bullet>();

        float newDamage = GetCritDamageWithChance(_equippedWeapon);

        bullet.Setup(attackDir, newDamage, _equippedWeapon.damageType, _ignoreTag, _equippedWeapon.speedOfBullet, _equippedWeapon.chanceOfEffect);

        if (_charType == CharType.player)
            CameraShake.Instance.ShakeCamera(2f, .2f);
        if (_equippedWeapon.autoAimBullet)
        {
            if (_targetTransform != null && !_targetTransform.CompareTag("CameraTarget"))
            {
                bullet.SetAutoAimTarget(_targetTransform);
            }
        }
        if (_equippedWeapon.weaponType == WeaponType.rocket)
        {
            bullet.SetExplosible(true, 2f);
        }
        Destroy(bulletTransform, _equippedWeapon.timeToDestroyBullet);
        yield return new WaitForSeconds(timeBetweenShots);
        if (bulletsAmount > 1)
        {
            _bulletsLeft -= 1;
            AnimateShooting();
            StartCoroutine(ShootBullet(timeBetweenShots, _bulletsLeft));
        }
        else
            _bulletsLeft = _bulletAmount;
    }
    
    
    private Transform CustomFindChild(string key, Transform parent)
    {
        Transform childFound = null;

        foreach (Transform child in parent)
        {
            if (child.name == key)
            {
                childFound = child;
            }
            else
            {
                if (child.childCount > 0)
                {
                    if (childFound == null)
                    {
                        childFound = CustomFindChild(key, child);
                    }
                }
            }
        }
        return childFound;
    }

    private float GetCritDamageWithChance(WeaponScriptableObject weapon)
    {
        //Debug.Log("WTF");
        float newDamage = weapon.damage + Random.Range(0, 5);
        float chanceOfDamage = Mathf.Round(Random.value * 100f) / 100f;

        //Debug.Log(chanceOfDamage + " / " + (equippedWeapon.chanceOfCrit + addativeCritChance + permanentCritChance));

        if (chanceOfDamage < weapon.chanceOfCrit + _addativeCritChance + _permanentCritChance)
        {
            Debug.Log("Crit chance worked, chance was:" + chanceOfDamage + " / " + (weapon.chanceOfCrit + _addativeCritChance + _permanentCritChance));

            newDamage *= Mathf.Round(Random.Range(1.2f, 2f) * 100f) / 100f;

            if (Random.value < 0.005f)
            {
                newDamage *= 10f;
                Debug.LogWarning("Super critical damage: " + newDamage);
            }
            //Debug.Log(newDamage);
        }
        //else
            //Debug.Log("Crit chance didn't work, chance was:" + chanceOfDamage + " / " + (weapon.chanceOfCrit + addativeCritChance + permanentCritChance));

        return newDamage;
    }

}

[Serializable]
public class WeaponSlot
{
    public void SetWeapon(WeaponScriptableObject weapon) => _weapon = weapon;

    private WeaponScriptableObject _weapon { set; get; }

    public WeaponScriptableObject weapon => _weapon;

    private bool _allowFire = true;

    public bool allowFire => _allowFire;

    public IEnumerator WaitToFireAgain()
    {
        _allowFire = false;

        Debug.Log(_weapon.name + " started reloading");

        yield return new WaitForSeconds(_weapon.firerate);

        _allowFire = true;
    }
}