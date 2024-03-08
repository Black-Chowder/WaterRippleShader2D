#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float2 iResolution;
float damping;
float deltaTime;

Texture2D Current;

sampler2D CurrentSampler = sampler_state
{
	Texture = <Current>;
};

Texture2D Previous;

sampler2D PreviousSampler = sampler_state
{
    Texture = <Previous>;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 texelSize = 1.0f / iResolution;
    float4 fragColor = tex2D(CurrentSampler, input.TextureCoordinates);
	
    float4 p_left = tex2D(PreviousSampler, input.TextureCoordinates + float2(-texelSize.x, 0));
    float4 p_right = tex2D(PreviousSampler, input.TextureCoordinates + float2(texelSize.x, 0));
    float4 p_top = tex2D(PreviousSampler, input.TextureCoordinates + float2(0, -texelSize.y));
    float4 p_bottom = tex2D(PreviousSampler, input.TextureCoordinates + float2(0, texelSize.y));
	
    fragColor = (p_left + p_right + p_top + p_bottom) / 2.0 - fragColor;
	
    fragColor *= damping;
	
    return fragColor;
}

technique SpriteDrawing
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};