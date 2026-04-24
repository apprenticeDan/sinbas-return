use super::user_error::UserError;

#[derive(Debug, Clone)]
pub struct Email(String);

impl Email {
    pub fn parse(value: String) -> Result<Self, UserError> {
        if !value.contains("@") { return Err(UserError::InvalidEmail); }
        Ok(Self(value))
    }

    pub fn value(&self) -> &str { &self.0 }

}
