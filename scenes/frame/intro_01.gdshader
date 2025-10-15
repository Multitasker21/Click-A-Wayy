shader_type canvas_item;

uniform float gamma = 1.2;

void fragment() {
    vec4 col = texture(TEXTURE, UV);
    COLOR = pow(col, vec4(gamma));
}