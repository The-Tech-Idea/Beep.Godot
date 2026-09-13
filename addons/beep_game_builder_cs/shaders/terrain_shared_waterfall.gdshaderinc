uniform bool shared_waterfall_enabled = false;
uniform sampler2D waterfall_noise : filter_nearest, repeat_disable;
uniform vec3 waterfall_base_bytes = vec3(35.0, 163.0, 192.0);
uniform float waterfall_offset = 0.0;
uniform float waterfall_rise = 64.0;
uniform int waterfall_kind = 2;
uniform int waterfall_projection = 0; // 0 square, 1 north-south iso, 2 west-east iso.

float fall_noise(float lane, float salt) {
    return texture(waterfall_noise, vec2((mod(lane,64.0)+0.5)/64.0,(salt+0.5)/8.0)).r;
}

vec3 shared_fall_color(vec2 pixel, float frame, vec3 fallback) {
    float cross = pixel.x;
    float y = pixel.y;
    if (waterfall_projection != 0) {
        float sx = waterfall_projection == 1 ? pixel.x : 96.0-pixel.x;
        cross = 2.0*(sx-32.0);
        float lip = 16.0+cross/4.0;
        if (cross>=0.0 && cross<64.0 && pixel.y>=lip && pixel.y<lip+waterfall_rise) {
            y = 64.0+pixel.y-lip;
        } else {
            cross = sx-64.0+2.0*pixel.y;
            y = 2.0*pixel.y-(sx-64.0);
            if (!(cross>=0.0 && cross<64.0 && y>=0.0 && y<64.0)) {
                cross = sx-64.0+2.0*(pixel.y-waterfall_rise);
                y = 2.0*(pixel.y-waterfall_rise)-(sx-64.0)+waterfall_rise;
            }
        }
    }
    float distance = min(waterfall_kind<=1 ? cross-10.94 : 100.0,
        waterfall_kind==0 || waterfall_kind==3 ? 53.06-cross : 100.0);
    if (distance<1.0) return fallback;
    float fade = clamp(distance/4.0,0.0,1.0);
    float gx = cross+mod(waterfall_offset,1536.0);
    vec3 color;
    if (y>=64.0 && y<64.0+waterfall_rise) {
        float light=0.0;
        float center=floor(gx/6.0);
        for (int i=-2;i<=2;i++) {
            float lane=center+float(i);
            float dx=gx-(lane*6.0+fall_noise(lane,0.0)*3.0);
            float cycles=2.0+floor(fall_noise(lane,1.0)*2.0);
            float dy=(fract((y-64.0)/48.0-frame/16.0*cycles+fall_noise(lane,2.0))-0.5)*48.0;
            vec2 q=vec2(dx/(0.7+fall_noise(lane,4.0)*0.5),dy/(5.0+fall_noise(lane,3.0)*7.0));
            light=max(light,exp(-dot(q,q))*(0.45+fall_noise(lane,5.0)*0.4));
        }
        color=floor(mix(waterfall_base_bytes*0.84,vec3(191.0,244.0,251.0),light*fade)+0.5);
    } else {
        float along=y<64.0?y:y-64.0-waterfall_rise;
        float light=0.0;
        for (int i=0;i<4;i++) {
            float lane=i==0?-13.0:(i==1?-4.0:(i==2?4.0:13.0));
            vec2 q=vec2((mod(along-frame*2.0-abs(lane)*1.7+16.0,32.0)-16.0)/1.25,(cross-32.0-lane)/3.2);
            light+=exp(-dot(q,q))*0.42;
        }
        color=floor(mix(waterfall_base_bytes,vec3(181.0,239.0,247.0),clamp(light,0.0,1.0)*fade)+0.5);
    }
    float foam=0.0;
    if (y>=59.0 && y<68.0) foam=clamp(1.0-abs(y-64.0)/5.0,0.0,1.0)*(0.45+0.25*sin(gx*3.14159265359/16.0-frame*3.14159265359/4.0));
    float py=y-64.0-waterfall_rise;
    if (py>=-min(10.0,waterfall_rise*0.5) && py<42.0) {
        float center=floor(gx/8.0);
        for (int i=-6;i<=6;i++) {
            float lane=center+float(i);
            float age=fract(frame/16.0+fall_noise(lane,7.0));
            float cy=-4.0*age*(1.0-age)*min(8.0,waterfall_rise*0.3)+age*12.0;
            float life=min(1.0,min(age*8.0,(1.0-age)*8.0));
            foam=max(foam,clamp(1.0-length(vec2((gx-lane*8.0-age*3.0)/(3.0+age*3.0),(py-cy)/(2.0+age*2.0))),0.0,1.0)*life);
        }
        float age=fract(frame/16.0);
        float broken=0.5+0.5*sin(gx*3.14159265359/8.0+age*6.28318530718);
        foam=max(foam,clamp(1.0-abs(py-8.0-age*28.0)/2.0,0.0,1.0)*sin(age*3.14159265359)*broken*0.5);
    }
    return floor(mix(color,vec3(240.0,254.0,255.0),foam*fade)+0.5)/255.0;
}
