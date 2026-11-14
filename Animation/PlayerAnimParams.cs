using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// CONTRATO entre o gameplay e o Animator do corpo. Todo Animator Controller do corpo deve usar estes nomes
    /// (o menu Tools > Horror FPS > Animation > Create Player Animator Controller cria um controller já com tudo). Parâmetros que não existirem no seu controller
    /// são simplesmente ignorados — dá para começar com poucos e ir acrescentando.
    /// </summary>
    public static class PlayerAnimParams
    {
        public struct Def
        {
            public string name;
            public AnimatorControllerParameterType type;
            public Def(string n, AnimatorControllerParameterType t) { name = n; type = t; }
        }

        // ---- floats
        /// <summary>Velocidade horizontal em m/s.</summary>
        public const string Speed = "Speed";
        /// <summary>Velocidade lateral (m/s, + = direita) — use como eixo X do blend tree 2D.</summary>
        public const string MoveX = "MoveX";
        /// <summary>Velocidade frontal (m/s, + = frente) — eixo Y do blend tree 2D.</summary>
        public const string MoveY = "MoveY";
        /// <summary>0 = em pé, 1 = totalmente agachado.</summary>
        public const string Crouch = "Crouch";
        public const string VerticalSpeed = "VerticalSpeed";
        /// <summary>Olhar vertical em graus (+ = para baixo).</summary>
        public const string Pitch = "Pitch";
        /// <summary>Esforço 0..1 (coração/corrida).</summary>
        public const string Exertion = "Exertion";
        /// <summary>Sanidade 0..1 (1 = lúcido).</summary>
        public const string Sanity = "Sanity";
        /// <summary>Ângulo (graus, -180..180) de onde veio o último golpe, relativo à frente do corpo.</summary>
        public const string HitDir = "HitDir";
        /// <summary>Giro horizontal em graus/s (para inclinar o corpo ao virar).</summary>
        public const string TurnRate = "TurnRate";
        /// <summary>Escalada: velocidade lateral (m/s).</summary>
        public const string ClimbX = "ClimbX";
        /// <summary>Escalada: velocidade vertical (m/s).</summary>
        public const string ClimbY = "ClimbY";
        /// <summary>
        /// +1 normal, -1 toca o clipe de trás para frente (andar de costas com o clipe de andar para frente).
        /// Use como "Multiplier" de velocidade (Speed > Parameter) nos estados Locomotion/Crouch.
        /// </summary>
        public const string Dir = "Dir";
        /// <summary>Escalada: velocidade de reprodução do clipe (-1.5..1.5; 0 = parado no degrau). Multiplicador de velocidade do estado Climb.</summary>
        public const string ClimbRate = "ClimbRate";

        // ---- bools
        public const string Grounded = "Grounded";
        public const string Sprint = "Sprint";
        public const string Climbing = "Climbing";
        public const string Dragging = "Dragging";
        public const string HoldingBreath = "HoldingBreath";
        public const string Dead = "Dead";
        public const string HasFlashlight = "HasFlashlight";
        public const string FlashlightOn = "FlashlightOn";

        // ---- ints
        /// <summary>0 nada, 1 genérico, 2 corpo a corpo, 3 pistola, 4 arma longa (ver enum HandItem).</summary>
        public const string HandItem = "HandItem";
        /// <summary>0 estável ... 4 quebrado.</summary>
        public const string SanityStage = "SanityStage";

        // ---- triggers
        public const string Jump = "Jump";
        public const string Land = "Land";
        public const string Attack = "Attack";
        public const string Fire = "Fire";
        public const string Reload = "Reload";
        public const string Equip = "Equip";
        public const string Hit = "Hit";
        public const string Interact = "Interact";
        public const string Gasp = "Gasp";
        /// <summary>Parada brusca depois de correr (toca o clipe "run to stop").</summary>
        public const string StopRun = "StopRun";

        // ---- layers (por nome)
        public const string LayerUpperBody = "Upper Body";
        public const string LayerFlashlight = "Flashlight";
        public const string LayerAdditive = "Additive";

        public static readonly int HSpeed = Animator.StringToHash(Speed);
        public static readonly int HMoveX = Animator.StringToHash(MoveX);
        public static readonly int HMoveY = Animator.StringToHash(MoveY);
        public static readonly int HCrouch = Animator.StringToHash(Crouch);
        public static readonly int HVerticalSpeed = Animator.StringToHash(VerticalSpeed);
        public static readonly int HPitch = Animator.StringToHash(Pitch);
        public static readonly int HExertion = Animator.StringToHash(Exertion);
        public static readonly int HSanity = Animator.StringToHash(Sanity);
        public static readonly int HHitDir = Animator.StringToHash(HitDir);
        public static readonly int HTurnRate = Animator.StringToHash(TurnRate);
        public static readonly int HClimbX = Animator.StringToHash(ClimbX);
        public static readonly int HClimbY = Animator.StringToHash(ClimbY);
        public static readonly int HDir = Animator.StringToHash(Dir);
        public static readonly int HClimbRate = Animator.StringToHash(ClimbRate);
        public static readonly int HGrounded = Animator.StringToHash(Grounded);
        public static readonly int HSprint = Animator.StringToHash(Sprint);
        public static readonly int HClimbing = Animator.StringToHash(Climbing);
        public static readonly int HDragging = Animator.StringToHash(Dragging);
        public static readonly int HHoldingBreath = Animator.StringToHash(HoldingBreath);
        public static readonly int HDead = Animator.StringToHash(Dead);
        public static readonly int HHasFlashlight = Animator.StringToHash(HasFlashlight);
        public static readonly int HFlashlightOn = Animator.StringToHash(FlashlightOn);
        public static readonly int HHandItem = Animator.StringToHash(HandItem);
        public static readonly int HSanityStage = Animator.StringToHash(SanityStage);
        public static readonly int HJump = Animator.StringToHash(Jump);
        public static readonly int HLand = Animator.StringToHash(Land);
        public static readonly int HAttack = Animator.StringToHash(Attack);
        public static readonly int HFire = Animator.StringToHash(Fire);
        public static readonly int HReload = Animator.StringToHash(Reload);
        public static readonly int HEquip = Animator.StringToHash(Equip);
        public static readonly int HHit = Animator.StringToHash(Hit);
        public static readonly int HInteract = Animator.StringToHash(Interact);
        public static readonly int HGasp = Animator.StringToHash(Gasp);
        public static readonly int HStopRun = Animator.StringToHash(StopRun);

        /// <summary>Lista completa (usada pelo menu que cria o controller e pela checagem de parâmetros ausentes).</summary>
        public static readonly Def[] All = new Def[]
        {
            new Def(Speed, AnimatorControllerParameterType.Float),
            new Def(MoveX, AnimatorControllerParameterType.Float),
            new Def(MoveY, AnimatorControllerParameterType.Float),
            new Def(Crouch, AnimatorControllerParameterType.Float),
            new Def(VerticalSpeed, AnimatorControllerParameterType.Float),
            new Def(Pitch, AnimatorControllerParameterType.Float),
            new Def(Exertion, AnimatorControllerParameterType.Float),
            new Def(Sanity, AnimatorControllerParameterType.Float),
            new Def(HitDir, AnimatorControllerParameterType.Float),
            new Def(TurnRate, AnimatorControllerParameterType.Float),
            new Def(ClimbX, AnimatorControllerParameterType.Float),
            new Def(ClimbY, AnimatorControllerParameterType.Float),
            new Def(Dir, AnimatorControllerParameterType.Float),
            new Def(ClimbRate, AnimatorControllerParameterType.Float),
            new Def(Grounded, AnimatorControllerParameterType.Bool),
            new Def(Sprint, AnimatorControllerParameterType.Bool),
            new Def(Climbing, AnimatorControllerParameterType.Bool),
            new Def(Dragging, AnimatorControllerParameterType.Bool),
            new Def(HoldingBreath, AnimatorControllerParameterType.Bool),
            new Def(Dead, AnimatorControllerParameterType.Bool),
            new Def(HasFlashlight, AnimatorControllerParameterType.Bool),
            new Def(FlashlightOn, AnimatorControllerParameterType.Bool),
            new Def(HandItem, AnimatorControllerParameterType.Int),
            new Def(SanityStage, AnimatorControllerParameterType.Int),
            new Def(Jump, AnimatorControllerParameterType.Trigger),
            new Def(Land, AnimatorControllerParameterType.Trigger),
            new Def(Attack, AnimatorControllerParameterType.Trigger),
            new Def(Fire, AnimatorControllerParameterType.Trigger),
            new Def(Reload, AnimatorControllerParameterType.Trigger),
            new Def(Equip, AnimatorControllerParameterType.Trigger),
            new Def(Hit, AnimatorControllerParameterType.Trigger),
            new Def(Interact, AnimatorControllerParameterType.Trigger),
            new Def(Gasp, AnimatorControllerParameterType.Trigger),
            new Def(StopRun, AnimatorControllerParameterType.Trigger),
        };
    }
}
