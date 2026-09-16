package srun

import (
	"crypto/hmac"
	"crypto/md5"
	"crypto/sha1"
	"encoding/hex"
	"encoding/json"
	"fmt"
)

func encryptPassword(challenge, password string) string {
	hash := hmac.New(md5.New, []byte(challenge))
	_, _ = hash.Write([]byte(password))
	return hex.EncodeToString(hash.Sum(nil))
}

func sha1Hex(data string) string {
	hash := sha1.New()
	_, _ = hash.Write([]byte(data))
	return hex.EncodeToString(hash.Sum(nil))
}

type userInfo struct {
	Username string `json:"username"`
	Password string `json:"password"`
	IP       string `json:"ip"`
	Acid     string `json:"acid"`
	EncVer   string `json:"enc_ver"`
}

func (info userInfo) encode(challenge string) (string, error) {
	payload, err := json.Marshal(info)
	if err != nil {
		return "", err
	}
	words := bytesToWords(payload, true)
	key := bytesToWords([]byte(challenge), false)
	for len(key) < 4 {
		key = append(key, 0)
	}
	if len(words) == 0 {
		return "", fmt.Errorf("empty login information")
	}

	const delta uint32 = 0x9E3779B9
	n := len(words) - 1
	z := words[n]
	var sum uint32
	rounds := 6 + 52/(n+1)
	for ; rounds > 0; rounds-- {
		sum += delta
		e := (sum >> 2) & 3
		for p := 0; p < n; p++ {
			y := words[p+1]
			mix := z>>5 ^ y<<2
			mix += y>>3 ^ z<<4 ^ (sum ^ y)
			mix += key[(uint32(p)&3)^e] ^ z
			words[p] += mix
			z = words[p]
		}
		y := words[0]
		mix := z>>5 ^ y<<2
		mix += y>>3 ^ z<<4 ^ (sum ^ y)
		mix += key[(uint32(n)&3)^e] ^ z
		words[n] += mix
		z = words[n]
	}

	encoded := customBase64(wordsToBytes(words))
	return "{SRBX1}" + encoded, nil
}

func bytesToWords(data []byte, includeLength bool) []uint32 {
	words := make([]uint32, 0, (len(data)+3)/4+1)
	for offset := 0; offset < len(data); offset += 4 {
		var word uint32
		for index := 0; index < 4 && offset+index < len(data); index++ {
			word |= uint32(data[offset+index]) << (8 * index)
		}
		words = append(words, word)
	}
	if includeLength {
		words = append(words, uint32(len(data)))
	}
	return words
}

func wordsToBytes(words []uint32) []byte {
	bytes := make([]byte, 0, len(words)*4)
	for _, word := range words {
		bytes = append(bytes, byte(word), byte(word>>8), byte(word>>16), byte(word>>24))
	}
	return bytes
}

const alphabet = "LVoJPiCN2R8G90yg+hmFHuacZ1OWMnrsSTXkYpUq/3dlbfKwv6xztjI7DeBE45QA"

func customBase64(source []byte) string {
	if len(source) == 0 {
		return ""
	}
	result := make([]byte, 0, ((len(source)+2)/3)*4)
	fullLength := len(source) - len(source)%3
	for index := 0; index < fullLength; index += 3 {
		value := uint32(source[index])<<16 | uint32(source[index+1])<<8 | uint32(source[index+2])
		result = append(result, alphabet[value>>18], alphabet[(value>>12)&63], alphabet[(value>>6)&63], alphabet[value&63])
	}
	switch len(source) - fullLength {
	case 1:
		value := uint32(source[fullLength]) << 16
		result = append(result, alphabet[value>>18], alphabet[(value>>12)&63], '=', '=')
	case 2:
		value := uint32(source[fullLength])<<16 | uint32(source[fullLength+1])<<8
		result = append(result, alphabet[value>>18], alphabet[(value>>12)&63], alphabet[(value>>6)&63], '=')
	}
	return string(result)
}
