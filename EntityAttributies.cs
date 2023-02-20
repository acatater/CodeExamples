using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;


public class EntityAttributies : MonoBehaviour, IDamagable
{
    public static Action<float> OnPlayerHealthChanged;

    public static Action<GameObject> OnEntitySpawned, OnEntityDespawned;

    public static Action OnEntityDie;

    [SerializeField]
    private UnityEvent _afterDeathPlayerEvent, _afterRessurectPlayerEvent;

    private float _maxHealth, _currentHealth;

    public bool Dead = false;
    public bool Flipped;

    public LayerMask LayerToIgnore;
    
    private bool _invincibleAlways, _invincible;
    private float _invincibleCooldown = 0f;

    private bool _stopDeath = false;

    private DamageWithType _damageWithType;
    private CharacterAnimation _characterAnim;
    private Rigidbody2D _rigidbody2D;

    private DamageType[] _resistanceToDamage;
    private EntityScriptableObject _entityData;

    private void Start()
    {
        _damageWithType = GetComponent<DamageWithType>();

        OnEntitySpawned?.Invoke(gameObject);
    }

    private IEnumerator LateStart()
    {
        yield return new WaitForSeconds(0.1f);

        if (_entityData.charType == CharType.player)
        {
            OnPlayerHealthChanged?.Invoke(_currentHealth / _maxHealth);
        }
    }
    private void FixedUpdate()
    {
        if (_invincibleCooldown > 0)
        {
            _invincibleCooldown -= Time.fixedDeltaTime;
            _invincible = true;
        }
        else if(!_invincibleAlways)
        {
            _invincible = false;
        }
    }

    public void SetInvincibility(bool invincible)
    {
        _invincibleAlways = invincible;
        invincible = invincible;
    }

    public void ApplyData(CharacterAnimation charAnim, Rigidbody2D rigidbody2D, EntityScriptableObject entityData)
    {
        _characterAnim = charAnim;
        _rigidbody2D = rigidbody2D;
        this._entityData = entityData;
        _resistanceToDamage = entityData.resistTo;

        float addativeSpeed = 0;
        if (entityData.charType != CharType.player)
            addativeSpeed = Random.Range(-0.2f, 1);

        if (entityData.charType == CharType.player)
            EventManager.onPlayerRessurect += StopDeadCoroutine;

        _maxHealth = entityData.maxHealth;
        _currentHealth = _maxHealth;
        GetComponent<WeaponManager>().SetCharType(entityData.charType);
        GetComponent<WeaponManager>().SetLayerToIgnore(LayerToIgnore);

        StartCoroutine(LateStart());
    }

    private void OnDisable()
    {
        EventManager.onPlayerRessurect -= StopDeadCoroutine;
    }

    public bool Heal(float value)
    {
        if (_currentHealth >= _maxHealth)
            return false;

        if (_currentHealth + value > _maxHealth)
        {
            _currentHealth = _maxHealth;
        }
        else
        {
            _currentHealth += value;
        }

        _characterAnim.AnimateHit(.15f, new Color(0, 1, 0.65f, 0.55f));

        if (_entityData.charType == CharType.player)
        {
            OnPlayerHealthChanged?.Invoke(_currentHealth / _maxHealth);
        }

        return true;
    }

    public void Damage(float damage, Vector3 bulletPosition, DamageType damageType, float chanceOfEffect, bool damageByEffect, bool damageFromPlayer = false)
    {

        if (_entityData.charType == CharType.player)
        {
            if (_invincible == true && !damageByEffect)
                return;
            else if (!damageByEffect)
            {
                _invincibleCooldown = .4f;
            }
        }

        _currentHealth -= damage;
        Knockback(bulletPosition);

        if (_entityData.charType == CharType.player)
        {
            OnPlayerHealthChanged?.Invoke(_currentHealth / _maxHealth);
        }
        else
        {
            if(damageFromPlayer)
                EventManager.onDamagePopup?.Invoke(transform, damage, true);
        }


        if (_currentHealth <= 0 && !Dead && _entityData.charType != CharType.player)
        {
            Dead = true;
            Die();
        }

        else if(_currentHealth <= 0 && !Dead && _entityData.charType == CharType.player)
        {
            Dead = true;
            StartCoroutine(WaitToDie());
        }

        int isAnyResistance = 0;

        if (damageType != DamageType.physicalDamage)
        {
            for (int i = 0; i < _resistanceToDamage.Length; i++)
            {
                if (_resistanceToDamage[i] != damageType)
                {
                    if (Random.value <= chanceOfEffect)
                    {
                        _damageWithType.ApplyDamage(damageType, chanceOfEffect);
                        isAnyResistance += 1;
                    }
                }
            }
        }
        if (isAnyResistance == 0 && damageType != DamageType.none)
            _characterAnim.AnimateHit(0.1f, Color.white);

    }

    private IEnumerator WaitToDie()
    {
        _characterAnim.animator.SetTrigger("Dead");
        Dead = true;
        _stopDeath = false;

        _afterDeathPlayerEvent?.Invoke();

        yield return new WaitForSeconds(2f);

        EventManager.Instance.ChangeToDeadScreen();

        yield return new WaitForSeconds(1f);

        if(_stopDeath)
            yield break;

        EventManager.onPlayerDie?.Invoke();
        //Debug.LogWarning("Died for some reason");
        Destroy(gameObject);
    }

    private void StopDeadCoroutine()
    {
        StopCoroutine(WaitToDie());

        Dead = false;
        _stopDeath = true;

        _afterRessurectPlayerEvent?.Invoke();

        Heal(_maxHealth);

        if (TryGetComponent(out WeaponManager weaponManager))
            weaponManager.AddEnergy(weaponManager.GetMaxEnergy);

        EventManager.Instance.CloseAllTabs();
    }

    private void Knockback(Vector3 bulletPosition)
    {
        if (bulletPosition != Vector3.zero)
        {
            Vector3 direction = (transform.position - bulletPosition).normalized;
            _rigidbody2D.AddForce(direction * 16f, ForceMode2D.Impulse);
        }
    }

    public void Die()
    {
        StopAllCoroutines();

        OnEntityDie?.Invoke();

        GameObject deadPrefab = Instantiate(_entityData.deadPrefab, transform.position, Quaternion.identity);
        Vector2 scale = deadPrefab.transform.localScale;

        if (_characterAnim.flipped)
        {
            scale.x = -1f;
        }
        if (!_characterAnim.flipped)
        {
            scale.x = 1f;
        }
        deadPrefab.transform.localScale = scale;
        Animator deadPrefabAnimator = deadPrefab.GetComponent<Animator>();
        deadPrefabAnimator.runtimeAnimatorController = _entityData.animator;
        deadPrefabAnimator.SetTrigger("Dead");

        if(_entityData.charType != CharType.player)
        {
            for (int f = 0; f < ParticlesData.Instance.dropThings.Count; f++)
            {
                if (Random.value > .5f)
                {
                    for (int i = 1; i <= Random.Range(1, 3); i++)
                        Instantiate(ParticlesData.Instance.dropThings[f], transform.position + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)), Quaternion.identity);
                }
            }
        }

        OnEntityDespawned?.Invoke(gameObject);

        Destroy(gameObject);
    }
}
