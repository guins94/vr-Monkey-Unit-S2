﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StealthPlayerController : Character {

    
    Camera cam;
    public Rigidbody rb;

    public GameObject normalModel;
    public GameObject cloakedModel;

    Renderer normalRenderer;
    Renderer cloakedRenderer;

    public bool hidden;
    private float hoverMaxHeight = 0;

    [Header("Movement Speed")]
    Vector3 inputVector;
    public bool infiniteFriction;
    public float friction = 3.0f;

    public bool infiniteAcceleration;
    public float acceleration = 3.0f;
    public float maxSpeed = 5.0f;
    Vector3 speed = Vector3.zero;

    public RobotThreadController threadController;
    public HoverSmokeController hoverSmokeController;
    public float threadWalkSpeed = 0.5f;
    public float threadRunSpeed = 0.75f;

    [Header("Rotation")]
    public float rotateSpeed = 5.0f;
    public bool instanteRotate = true;
    bool rotating = false;
    public bool lockMovementRotation = false;
    Quaternion rotationDestination;
    public bool lockMovementToAngle = false;
    public float maxAngleDiffToMove = 10.0f;
    float angleDiff;

    [Header("Energy")]

    bool moving = false;
    public bool enableEnergyDrain = false;
    public float maxEnergy = 50;
    public float energy = 50;
    public float lowEnergyFraction = 0.2f;
    public float lowEnergyMultiplier = 0.5f;
    public float energyDrainSpeed = 5;
    public float standingEnergyMultiplier = 0.5f;
    public ProgressBar energyBar;
    [Header("Skills")]
    public bool cloaked = false;
    public bool running = false;
    public float runningSpeedMultiplier = 1.5f;
    public float runningEnergyMultiplier = 1.5f;
    public float cloakingEnergyMultiplier = 3.5f;
    public float drainingEnergyMultiplier = 4.0f;
    public float shockDelay = 0.3f;
    public float shockCost = 10;
    public float drainSpeed = 0;
    public GameObject shockObject;
    public float shootCost = 2f;
    public float shootDelay = .25f;
    public float hoverDrainCost = 2f;
    public float hoverSpeed = .1f;
    public bool hover = false;
    public bool hoverCoolDown = false;

    public GameObject drainObject;
    public float drainRange = 2;
    public LayerMask enemyLayerMask;

    public AIAgent drainTarget;

    [Header("Effects")]
    public ParticleSystem walkParticles;
    public ParticleSystem runParticles;

    [Header("Upgrades")]
    public bool canShock = false;
    public bool canCloak = false;
    public bool canDrain = false;
    public bool canShoot = false;
    public bool canHover = false;


    public ParticleSystem warpParticles;
    

    static StealthPlayerController _instance;
    public static StealthPlayerController getInstance()
    {
        if (_instance == null)
        {
            _instance = GameObject.FindObjectOfType<StealthPlayerController>();
        }

        return _instance;
    }

    // Use this for initialization
    void Start () {
        audioSource = GetComponent<AudioSource>();
        cam = Camera.main;
        rb = GetComponent<Rigidbody>();

        normalRenderer = normalModel.GetComponent<Renderer>();
        cloakedRenderer = cloakedModel.GetComponent<Renderer>();

        if (energyBar != null)
        {
            energyBar.SetInitialValues(maxEnergy, 0, energy);
        }
        hoverMaxHeight = transform.position.y;
	}
	
    public void SetEnergy(float val)
    {
        energy = val;
        energyBar.UpdateBar(energy);
    }

    public void ResetEnergy()
    {
        energy = maxEnergy;
        energyBar.UpdateBar(energy);
        UpdateEnergyColor(energy / maxEnergy);
    }

    public void AddEnergy(float val)
    {
        energy += val;
        energy = Mathf.Min(maxEnergy, energy);
        energyBar.UpdateBar(energy);
        UpdateEnergyColor(energy / maxEnergy);
    }

    public void SpendEnergy(float val)
    {
        energy -= val;
        energy = Mathf.Min(maxEnergy, energy);
        energyBar.UpdateBar(energy);

        UpdateEnergyColor(energy/maxEnergy);
        if (energy <= 0)
        {
            GameLogic.instance.GameOver();
        }
    }

    void UpdateEnergyColor(float fraction)
    {
       normalRenderer.material.SetColor("_EmissionColor", Color.white * fraction);
       cloakedRenderer.material.SetColor("_EmissionColor", Color.white * fraction);
    }
    IEnumerator shockRoutine()
    {
        yield return new WaitForSeconds(shockDelay);
        SetState(States.idle);
    }

    IEnumerator shootRoutine()
    {
        Fire();
        yield return new WaitForSeconds(shootDelay);
        SetState(States.idle);
    }

    public void DrainOver()
    {
        SetState(States.idle);
        drainObject.SetActive(false);
    }

    public void StopDrain()
    {
        if (drainTarget != null)
        {
            drainTarget.OnDrainEnd();
        }
        
        SetState(States.idle);
        drainObject.SetActive(false);
    }

    public void StopMovement()
    {
        rb.velocity = Vector3.zero;
        if (runParticles.isPlaying)
        {
            runParticles.Stop();
        }
        if (walkParticles.isPlaying)
        {
            walkParticles.Stop();
        }
    }

    public IEnumerator HoveringRoutine()
    {
        while (energyLeft>0 && hover)
        {
            hover = !Input.GetButtonUp("Hover");
            hoverSmokeController.hover = hover;
            SpendEnergy(Time.deltaTime * hoverDrainCost);


            moving = false;
            inputVector = new Vector3(Input.GetAxis("Horizontal"), 1, Input.GetAxis("Vertical"));
            inputVector = Vector3.ClampMagnitude(inputVector, 1.0f);
            float camRotation = cam.transform.rotation.eulerAngles.y;
            inputVector = getMovementDirection(inputVector, camRotation);

            moving = true;
            if (inputVector.magnitude > 0.1f)
            {
                if (hoverMaxHeight >= transform.position.y)
                {
                    applyMovement(inputVector);
                } 
                rotate(inputVector);
            }
            else
            {
                if (hoverMaxHeight >= transform.position.y)
                {
                    applyMovement(new Vector3(0, 1, 0));
                } 
            }
            
            yield return null;
        }

        audioSource.PlayOneShot(AudioManager.getInstance().airBreak);

        StartCoroutine(HoverCoolDown());
        
        SetState(States.idle);

        IEnumerator HoverCoolDown()
        {
            
            hoverCoolDown = true;
            yield return new WaitForSeconds(2f);
            hoverCoolDown = false;
            
        }
    }

    // Update is called once per frame
    void Update() {
        if (GameLogic.instance.gameState != GameLogic.GameStates.gameplay)
        {
            return;
        }

        if (state == States.attacking)
        {
            if (!Input.GetButton("Drain")){
                StopDrain();
            }
        }

        if (canDrain && Input.GetButtonDown("Drain") && (state == States.idle || state == States.moving))
        {
            StopMovement();
            drainObject.SetActive(true);
            SetState(States.attacking);
            RaycastHit hit;
            if(Physics.Raycast(transform.position,transform.forward,out hit, drainRange, enemyLayerMask))
            {
                Debug.Log("Hit " + hit.transform.gameObject.name);
                AIAgent agent = hit.transform.gameObject.GetComponent<AIAgent>();
                if(agent!=null && agent.aiSight.sightState != AISight.SightStates.seeingEnemy){
                    agent.OnDrainStart();
                    Debug.Log("Draining ");
                   // drainObject.SetActive(true);
                   // SetState(States.attacking);
                    drainTarget = agent;
                }

            }else
            {
                Debug.Log("No hit");
            }
        }

        if (canShock && energy >= shockCost && Input.GetButtonDown("Shock") && (state == States.idle || state == States.moving))
        {

            SpendEnergy(shockCost);
            threadController.moving = false;
            SetState(States.shocking);
            StopMovement();
            GameObject thisShockObject = GameObject.Instantiate(shockObject);
            thisShockObject.transform.position = transform.position;
            StartCoroutine(shockRoutine());

        }

        if (canShoot && energy >= shootCost && Input.GetButtonDown("Shoot") && (state == States.idle || state == States.moving))
        {

            SpendEnergy(shootCost);
            SetState(States.shooting);
            StartCoroutine(shootRoutine());

        }

        if (canHover && Input.GetButtonDown("Hover") && (state == States.idle || state == States.moving))
        {
            if (!hoverCoolDown)
            {
                hoverMaxHeight = transform.position.y + 1.1f;
                hoverSmokeController.hover = true;
                hover = true;
                threadController.moving = false;
                SetState(States.hovering);
                StopMovement();
                StartCoroutine(HoveringRoutine());
            } 
            else audioSource.PlayOneShot(AudioManager.getInstance().airBreak);
            

        }

        


        if (canCloak && Input.GetButtonDown("Cloak") && (state == States.idle || state == States.moving))
        {
            normalModel.SetActive(false);
            cloakedModel.SetActive(true);
            cloaked = true;
            rb.velocity = Vector3.zero;
            threadController.moving = false;
        }
        else
        {
            if (cloaked && !Input.GetButton("Cloak"))
            {
                cloaked = false;
                normalModel.SetActive(true);
                cloakedModel.SetActive(false);
            }
        }
        


        if (state == States.idle && !cloaked)
        {
 
        

            moving = false;
            inputVector = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            inputVector = Vector3.ClampMagnitude(inputVector, 1f);
            float camRotation = cam.transform.rotation.eulerAngles.y;
            inputVector = getMovementDirection(inputVector, camRotation);

            //rb.velocity = new Vector3(Mathf.Max(0.0f,rb.velocity.x * (1.0f - Time.deltaTime)), rb.velocity.y, Mathf.Max(0.0f, rb.velocity.z * (1.0f - Time.deltaTime)));
            rb.velocity = new Vector3(0, rb.velocity.y, 0);


            if (Input.GetButton("Run"))
            {
                running = true;
                threadController.speed = threadRunSpeed;
                threadController.audioSource.pitch = 1.25f;
            }
            else
            {
                running = false;
                threadController.speed = threadWalkSpeed;
                threadController.audioSource.pitch = 1.0f;
            }

            if (inputVector.magnitude > 0.1f)
            {
                threadController.moving = true;

                if (!running)
                {
                    if (runParticles.isPlaying)
                    {
                        runParticles.Stop();
                    }
                    if (!walkParticles.isPlaying)
                    {
                        walkParticles.Play();
                    }
                    
                }
                else
                {
                    if (walkParticles.isPlaying)
                    {
                        walkParticles.Stop();
                    }
                    if (!runParticles.isPlaying)
                    {
                        runParticles.Play();
                    }
                    
                }


                moving = true;
                applyMovement(inputVector);
                rotate(inputVector);
            }
            else
            {
                if (walkParticles.isPlaying)
                {
                    walkParticles.Stop();
                }
                if (runParticles.isPlaying)
                {
                    runParticles.Stop();
                }
                
                threadController.moving = false;
            }
        }
        if (enableEnergyDrain)
        {
            float energyDrain= energyDrainSpeed * Time.deltaTime;

            if (energy <= maxEnergy * lowEnergyFraction)
            {
                energyDrain = energyDrain * lowEnergyMultiplier;
            }
            if (running)
            {
                energyDrain = energyDrain * runningEnergyMultiplier;
            }

            if (cloaked)
            {
                energyDrain = energyDrain * cloakingEnergyMultiplier;
            }

            if (!moving)
            {
                energyDrain = energyDrain * standingEnergyMultiplier;
            }

            if(state==States.attacking)
            {
                if (drainTarget == null)
                {
                    energyDrain = energyDrain * drainingEnergyMultiplier;
                }
                else
                {
                    energyDrain = 0;
                }
                
            }

            energy -= energyDrain;
            energyBar.UpdateBar(energy);
            UpdateEnergyColor(energy / maxEnergy);
            if (energy <= 0)
            {
                GameLogic.instance.GameOver();
            }
        }
        
    }

    public ParticleSystem damageParticle;

    public override void DealDamage(float val)
    {
        audioSource.PlayOneShot(AudioManager.getInstance().playerHit);
        damageParticle.Play();

        base.DealDamage(val);
        SpendEnergy(val);
    }

    public Vector3 getMovementDirection(Vector3 direction, float cameraAngle = 0)
    {
        float sin = Mathf.Sin((Mathf.PI / 180.0f) * cameraAngle);
        float cos = Mathf.Cos((Mathf.PI / 180.0f) * cameraAngle);

        float resultX = cos * direction.x + sin * direction.z;
        float resultZ = cos * direction.z - sin * direction.x;

        Vector3 result = new Vector3(resultX,0, resultZ);
        return result;
    }

    public void applyMovement(Vector3 direction)
    {
        float speedToUse = maxSpeed;

        if (running)
        {
            speedToUse = speedToUse * runningSpeedMultiplier;
        }

        Vector3 newSpeed = Vector3.zero;
        if (infiniteAcceleration && direction.magnitude > 0.1f)
        {
            newSpeed = speedToUse * new Vector3(direction.x, hoverSpeed, direction.z);
        }
        else {
            newSpeed = Vector3.Lerp(speed, speedToUse * new Vector3(direction.x, hoverSpeed, direction.z), acceleration * Time.deltaTime);
        }
        Debug.Log(newSpeed.y + rb.velocity.y);
        
        if (!hover) newSpeed = new Vector3(newSpeed.x, rb.velocity.y, newSpeed.z);

        speed = newSpeed;

        


        if (rb == null)
        {
            transform.position += Time.deltaTime * newSpeed;
        }
        else {
            rb.velocity = newSpeed;

        }
    }

    public void rotate(Vector3 direction3d)
    {
        Vector2 direction = new Vector2(direction3d.x, direction3d.z);
        if (Vector3.Cross(new Vector3(direction.x, direction.y, 0), new Vector3(0, 1, 0)).z > 0)
        {
            if (instanteRotate)
            {
                transform.rotation = Quaternion.AngleAxis(Vector2.Angle(Vector2.up, direction), Vector3.up);

            }
            else {
                rotationDestination = Quaternion.AngleAxis(Vector2.Angle(Vector2.up, direction), Vector3.up);

                angleDiff = transform.rotation.eulerAngles.y - rotationDestination.eulerAngles.y;

                if (angleDiff > 190)
                {
                    angleDiff -= 360;
                }

                transform.rotation = Quaternion.Lerp(transform.rotation, rotationDestination, rotateSpeed * Time.deltaTime);
            }
        }
        else {

            if (instanteRotate)
            {
                transform.rotation = Quaternion.AngleAxis(-Vector2.Angle(Vector2.up, direction), Vector3.up);
            }
            else {
                rotationDestination = Quaternion.AngleAxis(-Vector2.Angle(Vector2.up, direction), Vector3.up);

                angleDiff = transform.rotation.eulerAngles.y - rotationDestination.eulerAngles.y;

                if (angleDiff > 190)
                {
                    angleDiff -= 360;
                }
                transform.rotation = Quaternion.Lerp(transform.rotation, rotationDestination, rotateSpeed * Time.deltaTime);
            }
        }
    }

    public void Kill()
    {

    }
}
